using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using iptv.Services._Resolver;
using iptv.Services._Stream.Urls;
using Microsoft.Extensions.Logging;
using Utilities.Constants;

namespace iptv.Services._IptvProvider.Fetchers;

/// <summary>
/// Official-source ladder for channels without public static streams (tokenized official players).
/// Reads the embedded resolvers.json; one stream per entry:
///   resolve        → StreamUri = pageUrl, resolved on demand by Stream/ResolveAsync;
///   youtube        → the official embed URL (YouTubeLive method);
///   clientResolve  → the app resolves pageUrl from the user's own IP (IP-bound tokens);
///   officialPlayer → the broadcaster's own live page / embed.
/// Invalid entries are logged (once per sync) and skipped; this never throws.
/// </summary>
public class ResolverProviderFetcher(ILogger<ResolverProviderFetcher> _logger)
    : IProviderFetcher, RegisterMode.IScopedDependency
{
    public ProviderKind Kind => ProviderKind.Resolver;

    public Task<ProviderFetchResult> FetchAsync(IptvProviders provider, CancellationToken cancellationToken)
    {
        ResolverCatalogRoot catalog;
        try
        {
            catalog = ResolverCatalog.LoadEmbedded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resolver catalog could not be read; no resolver streams this sync.");
            catalog = new ResolverCatalogRoot();
        }

        var result = BuildResult(provider.PublicKey, catalog,
            (entry, reason) => _logger.LogWarning(
                "Resolver entry {CanonicalId} ({Type}) skipped: {Reason}", entry.CanonicalId, entry.Type, reason));

        return Task.FromResult(result);
    }

    internal static ProviderFetchResult BuildResult(
        string providerPublicKey, ResolverCatalogRoot catalog, Action<ResolverEntry, string> onInvalid)
    {
        var result = new ProviderFetchResult();
        var channels = new Dictionary<string, ExternalChannel>(StringComparer.Ordinal);

        foreach (var e in catalog.Resolvers ?? [])
        {
            var error = TryBuildStream(providerPublicKey, e, out var stream);
            if (error != null)
            {
                onInvalid?.Invoke(e, error);
                continue;
            }

            var id = e.CanonicalId.Trim();
            if (!channels.ContainsKey(id))
            {
                channels[id] = new ExternalChannel
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(e.Name) ? id : e.Name.Trim(),
                    CanonicalIdHint = id,
                    TvgId = id,
                    Country = e.Country ?? CountryFromId(id),
                    Logo = e.Logo,
                    Categories = []
                };
            }
            else if (string.IsNullOrWhiteSpace(channels[id].Logo) && !string.IsNullOrWhiteSpace(e.Logo))
            {
                channels[id].Logo = e.Logo;
            }

            result.Streams.Add(stream);
        }

        result.Channels = [.. channels.Values];
        return result;
    }

    private static string TryBuildStream(string providerPublicKey, ResolverEntry e, out ExternalStream stream)
    {
        stream = null;

        if (string.IsNullOrWhiteSpace(e.CanonicalId))
            return "missing canonicalId";

        var type = !string.IsNullOrWhiteSpace(e.Type)
            ? e.Type.Trim()
            : e.Method == ResolverMethods.YouTubeLive ? StreamTypes.YouTube : StreamTypes.Resolve;

        string url;
        switch (type)
        {
            case StreamTypes.YouTube:
                url = YouTubeUrls.VideoEmbed(e.YouTubeVideoId) ?? YouTubeUrls.ChannelLiveEmbed(e.YouTubeChannelId);
                if (url == null)
                    return "YouTubeLive entry needs a valid youtubeVideoId or youtubeChannelId";
                break;

            case StreamTypes.Resolve:
            case StreamTypes.ClientResolve:
                if (!IsHttpUrl(e.PageUrl) && !IsHttpUrl(e.ApiUrl))
                    return "pageUrl/apiUrl missing or not http(s)";
                if (string.IsNullOrWhiteSpace(e.Pattern))
                    return "pattern missing";
                if (e.Method is not (ResolverMethods.HtmlRegex or ResolverMethods.JsonApi))
                    return $"unsupported method '{e.Method}'";
                if (e.Method == ResolverMethods.HtmlRegex && !IsValidRegex(e.Pattern))
                    return "pattern is not a valid regex with a named group 'url'";
                url = IsHttpUrl(e.PageUrl) ? e.PageUrl.Trim() : e.ApiUrl.Trim();
                break;

            case StreamTypes.OfficialPlayer:
                url = IsHttpUrl(e.PlayerUrl) ? e.PlayerUrl.Trim() : IsHttpUrl(e.PageUrl) ? e.PageUrl.Trim() : null;
                if (url == null)
                    return "playerUrl/pageUrl missing or not http(s)";
                break;

            default:
                return $"unsupported type '{type}'";
        }

        var headers = (e.Headers ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h.Key) && !string.IsNullOrWhiteSpace(h.Value))
            .ToDictionary(h => h.Key.Trim(), h => h.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        var ladder = type is StreamTypes.Resolve or StreamTypes.ClientResolve;

        stream = new ExternalStream
        {
            Channel = e.CanonicalId.Trim(),
            Title = $"{e.Name ?? e.CanonicalId} ({type})",
            Url = url,
            Type = type,
            RequiredRegion = string.IsNullOrWhiteSpace(e.RequiredRegion) ? null : e.RequiredRegion.Trim(),
            PageUrl = ladder ? e.PageUrl : null,
            ResolveMethod = ladder ? e.Method : null,
            ResolvePattern = ladder ? e.Pattern : null,
            ResolveApiUrl = ladder ? e.ApiUrl : null,
            ResolveHeaders = ladder && headers.Count > 0 ? headers : null,
            ResolveTtlSeconds = ladder ? Math.Max(0, e.TtlSeconds) : 0,
            IpBound = e.IpBound,
            PlayerUrl = type == StreamTypes.OfficialPlayer ? url : null,
            Embeddable = type == StreamTypes.OfficialPlayer ? e.Embeddable : null,
            Quality = "Auto",
            StableExternalId = StableId(providerPublicKey, e.CanonicalId.Trim(), type)
        };
        return null;
    }

    internal static string StableId(string providerPublicKey, string canonicalId, string type)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{providerPublicKey}|{canonicalId}|{type}")));

    private static bool IsHttpUrl(string url)
        => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) &&
           (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            return new Regex(pattern).GetGroupNames().Contains("url");
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string CountryFromId(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot > 0 && id.Length - dot - 1 == 2 ? id[(dot + 1)..].ToUpperInvariant() : null;
    }
}
