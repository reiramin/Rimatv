using System.Text;
using System.Text.RegularExpressions;
using iptv.Services._Canonical;

namespace iptv.Services._IptvProvider.Fetchers;

public sealed class M3uEntry
{
    public string ChannelId { get; set; }
    public string CanonicalIdHint { get; set; }
    public string Feed { get; set; }
    public string Name { get; set; }
    public string Logo { get; set; }
    public string GroupTitle { get; set; }
    public string Country { get; set; }
    public string Language { get; set; }
    public string Quality { get; set; }
    public string Url { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public List<string> Labels { get; set; } = [];
    public bool RequiresIranianIp { get; set; }
    public bool GeoBlocked { get; set; }
}

/// <summary>
/// Robust EXTM3U parser. Handles tvg-* attributes, #EXTVLCOPT http-user-agent/http-referrer,
/// title after the last comma, multiple streams per channel, tvg-id of the form channel@feed,
/// and name decorations (Ⓢ Ⓖ Ⓨ Ⓣ, [IR], [Geo-blocked], …) which are stripped but recorded as labels.
/// Pure and deterministic so it can be unit tested without any network.
/// </summary>
public static partial class M3uPlaylistParser
{
    [GeneratedRegex("""(\S+?)="([^"]*)"|(\S+?)='([^']*)'""")]
    private static partial Regex AttributePattern();

    // Bracketed decorations, e.g. [IR], [Geo-blocked], [Not 24/7].
    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex BracketPattern();

    // Decoration symbols: Ⓢ SD, Ⓖ geo-blocked, Ⓨ YouTube, Ⓣ / Ⓐ etc.
    private const string DecorationSymbols = "ⓈⓖⒼⓎⓉⒶⒽ";

    public static List<M3uEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<M3uEntry>();

        string extinf = null;
        string pendingUserAgent = null;
        string pendingReferer = null;

        foreach (var rawLine in lines)
        {
            if (rawLine == null)
                continue;

            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                extinf = line;
                pendingUserAgent = null;
                pendingReferer = null;
                continue;
            }

            if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
            {
                var opt = line["#EXTVLCOPT:".Length..];
                var eq = opt.IndexOf('=');
                if (eq > 0)
                {
                    var key = opt[..eq].Trim();
                    var value = opt[(eq + 1)..].Trim();
                    if (key.Equals("http-user-agent", StringComparison.OrdinalIgnoreCase))
                        pendingUserAgent = value;
                    else if (key.Equals("http-referrer", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("http-referer", StringComparison.OrdinalIgnoreCase))
                        pendingReferer = value;
                }
                continue;
            }

            // Other directives (e.g. #EXTGRP, #KODIPROP) are ignored but must not reset EXTINF.
            if (line.StartsWith('#'))
                continue;

            // A URL line completes the current entry.
            if (extinf == null)
                continue;

            var entry = BuildEntry(extinf, line, pendingUserAgent, pendingReferer);
            if (entry != null)
                entries.Add(entry);

            extinf = null;
            pendingUserAgent = null;
            pendingReferer = null;
        }

        return entries;
    }

    private static M3uEntry BuildEntry(string extinf, string url, string userAgent, string referer)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Split "#EXTINF:-1 <attrs>,<title>" — title is everything after the LAST comma.
        var body = extinf["#EXTINF:".Length..];
        var lastComma = body.LastIndexOf(',');
        var attrsPart = lastComma >= 0 ? body[..lastComma] : body;
        var rawTitle = lastComma >= 0 ? body[(lastComma + 1)..].Trim() : string.Empty;

        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttributePattern().Matches(attrsPart))
        {
            var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value;
            var value = m.Groups[1].Success ? m.Groups[2].Value : m.Groups[4].Value;
            attrs[key] = value;
        }

        var tvgId = attrs.GetValueOrDefault("tvg-id")?.Trim();
        var tvgName = attrs.GetValueOrDefault("tvg-name")?.Trim();

        var displayName = !string.IsNullOrWhiteSpace(rawTitle) ? rawTitle : tvgName ?? string.Empty;

        var (cleanName, labels, requiresIr, geoBlocked) = CleanName(displayName);

        string canonicalHint = null;
        string feed = null;
        if (!string.IsNullOrWhiteSpace(tvgId))
        {
            var at = tvgId.IndexOf('@');
            if (at > 0)
            {
                canonicalHint = tvgId[..at];
                feed = tvgId[(at + 1)..];
            }
            else
            {
                canonicalHint = tvgId;
            }
        }

        var channelId = !string.IsNullOrWhiteSpace(tvgId)
            ? tvgId
            : $"m3u:{ChannelNameNormalizer.Normalize(cleanName)}";

        // Country tag inside the name (e.g. "[IR]") also means requires-Iranian-IP handled above.
        var country = attrs.GetValueOrDefault("tvg-country")?.Trim();

        return new M3uEntry
        {
            ChannelId = channelId,
            CanonicalIdHint = canonicalHint,
            Feed = feed,
            Name = string.IsNullOrWhiteSpace(cleanName) ? channelId : cleanName,
            Logo = attrs.GetValueOrDefault("tvg-logo")?.Trim(),
            GroupTitle = attrs.GetValueOrDefault("group-title")?.Trim(),
            Country = country,
            Language = attrs.GetValueOrDefault("tvg-language")?.Trim(),
            Quality = attrs.GetValueOrDefault("tvg-quality")?.Trim(),
            Url = url.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            Referer = string.IsNullOrWhiteSpace(referer) ? null : referer,
            Labels = labels,
            RequiresIranianIp = requiresIr,
            GeoBlocked = geoBlocked
        };
    }

    private static (string Name, List<string> Labels, bool RequiresIr, bool GeoBlocked) CleanName(string name)
    {
        var labels = new List<string>();
        var requiresIr = false;
        var geoBlocked = false;

        if (string.IsNullOrWhiteSpace(name))
            return (string.Empty, labels, false, false);

        // Bracketed tags.
        foreach (Match m in BracketPattern().Matches(name))
        {
            var tag = m.Value.Trim('[', ']', ' ');
            if (tag.Length == 0)
                continue;

            labels.Add(tag);

            if (tag.Equals("IR", StringComparison.OrdinalIgnoreCase) ||
                tag.Contains("Iran", StringComparison.OrdinalIgnoreCase))
                requiresIr = true;

            if (tag.Contains("Geo", StringComparison.OrdinalIgnoreCase))
                geoBlocked = true;
        }

        var stripped = BracketPattern().Replace(name, " ");

        var sb = new StringBuilder(stripped.Length);
        foreach (var ch in stripped)
        {
            if (DecorationSymbols.IndexOf(ch) >= 0)
            {
                if (ch is 'Ⓖ' or 'ⓖ')
                    geoBlocked = true;
                labels.Add(ch.ToString());
                continue;
            }
            sb.Append(ch);
        }

        var cleaned = Regex.Replace(sb.ToString(), @"\s+", " ").Trim(' ', '-', '|', '•');

        return (cleaned, labels, requiresIr, geoBlocked);
    }
}
