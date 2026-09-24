using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace iptv.Services._Resolver;

/// <summary>Pure helpers used by the server-side resolver (and mirrored by the validator).</summary>
public static partial class ResolverExtraction
{
    public static readonly TimeSpan MaxCache = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ExpirySafety = TimeSpan.FromSeconds(60);

    // Query parameters that carry the requester's address in common CDN token schemes.
    private static readonly HashSet<string> IpParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ip", "ipaddr", "ip_address", "ipaddress", "clientip", "client_ip", "userip", "user_ip",
        "cip", "uip", "viewerip", "remote_ip"
    };

    private static readonly string[] ExpiryParamNames =
        ["expires", "expire", "expiry", "exp", "e", "validto", "valid_to", "token_expires", "hdnea_exp"];

    /// <summary>Extracts the stream URL, made absolute against <paramref name="baseUrl"/>; null when not found.</summary>
    public static string ExtractUrl(string content, string method, string pattern, string baseUrl)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrWhiteSpace(pattern))
            return null;

        var raw = method switch
        {
            ResolverMethods.JsonApi => ExtractJsonPath(content, pattern),
            _ => ExtractRegex(content, pattern)
        };

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim()
            .Replace("\\/", "/")
            .Replace("\\u0026", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&");
        cleaned = WebUtility.HtmlDecode(cleaned);

        if (cleaned.StartsWith("//"))
            cleaned = "https:" + cleaned;

        if (Uri.TryCreate(cleaned, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return abs.ToString();

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var b) && Uri.TryCreate(b, cleaned, out var rel))
            return rel.ToString();

        return null;
    }

    private static string ExtractRegex(string content, string pattern)
    {
        try
        {
            var m = Regex.Match(content, pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            if (!m.Success) return null;
            var g = m.Groups["url"];
            return g.Success ? g.Value : null;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>Minimal JSONPath: <c>$.a.b[0].c</c> (dotted members and array indexes).</summary>
    public static string ExtractJsonPath(string json, string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement;

            foreach (var token in JsonPathTokens().Matches(path.Trim().TrimStart('$')).Select(m => m.Value))
            {
                if (token.StartsWith('['))
                {
                    var i = int.Parse(token.Trim('[', ']'));
                    if (current.ValueKind != JsonValueKind.Array || i >= current.GetArrayLength())
                        return null;
                    current = current[i];
                }
                else
                {
                    var name = token.TrimStart('.');
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                        return null;
                }
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\.[^.\[]+|\[\d+\]")]
    private static partial Regex JsonPathTokens();

    /// <summary>True when the body is an HLS playlist.</summary>
    public static bool IsPlaylist(string body)
        => body != null && body.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("#EXTM3U", StringComparison.Ordinal);

    /// <summary>
    /// A token is IP-bound when the entry says so (e.g. a hashed-IP parameter found in research) or
    /// when the resolved URL carries a literal IP address in a query parameter. The token was issued
    /// to the SERVER's egress address — whatever the caller's IP is — so it will not play for the user.
    /// </summary>
    public static bool IsIpBound(string url, bool declaredIpBound)
    {
        if (declaredIpBound)
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        foreach (var (key, value) in Query(uri))
        {
            if (!IPAddress.TryParse(value, out var ip) || !LooksLikeLiteral(value, ip))
                continue;

            if (IpParamNames.Contains(key) || ip.AddressFamily == AddressFamily.InterNetwork)
                return true;
        }

        return false;
    }

    // IPAddress.TryParse accepts "1" or "123"; require a dotted quad or an IPv6 literal.
    private static bool LooksLikeLiteral(string value, IPAddress ip)
        => ip.AddressFamily == AddressFamily.InterNetworkV6 ? value.Contains(':') : value.Count(c => c == '.') == 3;

    /// <summary>
    /// Token expiry from common query parameters (unix seconds or ms, Akamai hdnts exp=). With
    /// <paramref name="now"/>, an expiry already in the past is ignored: some CDNs (e.g. Show TV's)
    /// carry a stale e= value they do not enforce.
    /// </summary>
    public static DateTime? ParseExpiry(string url, DateTime? now = null)
    {
        var expiry = ParseExpiryRaw(url);
        return now.HasValue && expiry <= now.Value ? null : expiry;
    }

    private static DateTime? ParseExpiryRaw(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var query = Query(uri).ToList();

        foreach (var name in ExpiryParamNames)
        foreach (var (key, value) in query)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && TryUnix(value, out var t))
                return t;

        // Akamai: hdnts=st=...~exp=1712345678~acl=...
        foreach (var (key, value) in query)
            if (key.StartsWith("hdn", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(value, @"exp=(\d{10,13})");
                if (m.Success && TryUnix(m.Groups[1].Value, out var t))
                    return t;
            }

        return null;
    }

    private static bool TryUnix(string value, out DateTime moment)
    {
        moment = default;
        if (!long.TryParse(value, out var n))
            return false;
        if (n > 100_000_000_000) n /= 1000;              // milliseconds
        if (n < 1_000_000_000 || n > 99_999_999_999) return false;
        moment = DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
        return true;
    }

    /// <summary>min(ttlSeconds, tokenExpiry − 60 s, 15 min); zero or less means "do not cache".</summary>
    public static TimeSpan CacheDuration(int ttlSeconds, DateTime? tokenExpiry, DateTime now)
    {
        var d = MaxCache;
        if (ttlSeconds > 0 && TimeSpan.FromSeconds(ttlSeconds) < d)
            d = TimeSpan.FromSeconds(ttlSeconds);
        if (tokenExpiry.HasValue && tokenExpiry.Value - ExpirySafety - now < d)
            d = tokenExpiry.Value - ExpirySafety - now;
        return d;
    }

    private static IEnumerable<(string Key, string Value)> Query(Uri uri)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            yield return (Uri.UnescapeDataString(part[..eq]), Uri.UnescapeDataString(part[(eq + 1)..]));
        }
    }
}
