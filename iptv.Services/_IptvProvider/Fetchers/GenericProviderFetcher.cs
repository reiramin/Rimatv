using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using Utilities.Constants;

namespace iptv.Services._IptvProvider.Fetchers;

/// <summary>
/// iptv-org JSON shape. Reads channels/streams/logos and (optionally) feeds.json and blocklist.json.
/// Tolerates the 2026-09-18 `label`→`labels` change, sources logos from logos.json (prefer in_use,
/// no feed, largest width), attaches per-feed languages, and splits a channel into channel@feed when
/// a feed's language set differs from the channel's main feed (so e.g. Al Jazeera Arabic/English do
/// not merge).
/// </summary>
public class GenericProviderFetcher(IHttpClientFactory httpClientFactory)
    : ProviderFetcherBase(httpClientFactory), IProviderFetcher, RegisterMode.IScopedDependency
{
    public ProviderKind Kind => ProviderKind.Generic;

    public async Task<ProviderFetchResult> FetchAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        using var client = CreateClient(provider);

        var channels = await FetchJsonAsync<List<ExternalChannel>>(
            client, provider, provider.ChannelsEndpoint, cancellationToken) ?? [];

        List<ExternalStream> streams = [];
        if (!string.IsNullOrWhiteSpace(provider.StreamsEndpoint))
            streams = await FetchJsonAsync<List<ExternalStream>>(
                client, provider, provider.StreamsEndpoint, cancellationToken) ?? [];

        List<ExternalLogo> logos = [];
        if (!string.IsNullOrWhiteSpace(provider.LogosEndpoint))
            logos = await FetchJsonAsync<List<ExternalLogo>>(
                client, provider, provider.LogosEndpoint, cancellationToken) ?? [];

        List<ExternalFeed> feeds = [];
        if (!string.IsNullOrWhiteSpace(provider.FeedsEndpoint))
            feeds = await FetchJsonAsync<List<ExternalFeed>>(
                client, provider, provider.FeedsEndpoint, cancellationToken) ?? [];

        List<ExternalBlocklistEntry> blocklist = [];
        if (!string.IsNullOrWhiteSpace(provider.BlocklistEndpoint))
            blocklist = await FetchJsonAsync<List<ExternalBlocklistEntry>>(
                client, provider, provider.BlocklistEndpoint, cancellationToken) ?? [];

        return BuildResult(channels, streams, logos, feeds, blocklist);
    }

    internal static ProviderFetchResult BuildResult(
        List<ExternalChannel> channels,
        List<ExternalStream> streams,
        List<ExternalLogo> logos,
        List<ExternalFeed> feeds,
        List<ExternalBlocklistEntry> blocklist)
    {
        var result = new ProviderFetchResult();

        foreach (var b in blocklist)
            if (!string.IsNullOrWhiteSpace(b.Channel))
                result.BlockedChannelIds.Add(b.Channel.Trim());

        // Best logo per channel: prefer in_use, then no feed, then largest width.
        var logoByChannel = logos
            .Where(l => !string.IsNullOrWhiteSpace(l.Channel) && !string.IsNullOrWhiteSpace(l.Logo))
            .GroupBy(l => l.Channel.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(l => l.InUse)
                      .ThenBy(l => string.IsNullOrWhiteSpace(l.Feed) ? 0 : 1)
                      .ThenByDescending(l => l.Width)
                      .First().Logo.Trim(),
                StringComparer.Ordinal);

        // Feed languages keyed by (channel, feedId) and the main-feed language set per channel.
        var feedLangs = new Dictionary<(string, string), List<string>>();
        var mainLangsByChannel = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var f in feeds)
        {
            if (string.IsNullOrWhiteSpace(f.Channel) || string.IsNullOrWhiteSpace(f.Id))
                continue;
            var ch = f.Channel.Trim();
            var langs = (f.Languages ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            feedLangs[(ch, f.Id.Trim())] = langs;
            if (f.IsMain)
                mainLangsByChannel[ch] = langs;
        }

        var channelById = channels
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .GroupBy(c => c.Id.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Emit channels lazily as streams reference (base or channel@feed) keys.
        var emitted = new Dictionary<string, ExternalChannel>(StringComparer.Ordinal);

        foreach (var s in streams)
        {
            if (string.IsNullOrWhiteSpace(s.Url) || string.IsNullOrWhiteSpace(s.Channel))
                continue;

            var baseId = s.Channel.Trim();
            if (!channelById.TryGetValue(baseId, out var baseChannel))
                continue; // unmapped stream.channel

            var feedId = string.IsNullOrWhiteSpace(s.Feed) ? null : s.Feed.Trim();
            var langs = feedId != null && feedLangs.TryGetValue((baseId, feedId), out var fl)
                ? fl
                : [];

            var mainLangs = mainLangsByChannel.GetValueOrDefault(baseId, []);

            var divergent = feedId != null && langs.Count > 0 && mainLangs.Count > 0 &&
                            !new HashSet<string>(langs, StringComparer.OrdinalIgnoreCase)
                                .SetEquals(mainLangs);

            var key = divergent ? $"{baseId}@{feedId}" : baseId;

            if (!emitted.ContainsKey(key))
            {
                var name = divergent
                    ? $"{baseChannel.Name} {feedId}".Trim()
                    : baseChannel.Name;

                emitted[key] = new ExternalChannel
                {
                    Id = key,
                    Name = name,
                    Country = baseChannel.Country,
                    Categories = baseChannel.Categories ?? [],
                    Logo = logoByChannel.GetValueOrDefault(baseId) ?? baseChannel.Logo,
                    AltNames = baseChannel.AltNames ?? [],
                    Network = baseChannel.Network,
                    IsNsfw = baseChannel.IsNsfw,
                    Closed = baseChannel.Closed,
                    ReplacedBy = baseChannel.ReplacedBy,
                    CanonicalIdHint = key,
                    Feed = divergent ? feedId : null,
                    Languages = langs.Count > 0 ? langs : mainLangs,
                    TvgId = baseId
                };
            }

            var labels = s.ResolveLabels();
            var geoBlocked = labels.Any(l => l.Contains("Geo", StringComparison.OrdinalIgnoreCase));

            result.Streams.Add(new ExternalStream
            {
                Channel = key,
                Feed = feedId,
                Title = s.Title,
                Url = s.Url,
                UserAgent = s.UserAgent,
                Referrer = s.Referrer,
                Quality = s.Quality,
                Labels = labels,
                Languages = langs,
                GeoBlocked = geoBlocked
            });
        }

        result.Channels = [.. emitted.Values];
        return result;
    }
}
