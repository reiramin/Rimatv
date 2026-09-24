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

        // Streams first: only channels/logos/feeds referenced by a stream are kept, and every file
        // is deserialized item by item from the response stream (never held as a string).
        List<ExternalStream> streams = [];
        if (!string.IsNullOrWhiteSpace(provider.StreamsEndpoint))
            await foreach (var st in FetchJsonItemsAsync<ExternalStream>(
                               client, provider, provider.StreamsEndpoint, cancellationToken))
                if (!string.IsNullOrWhiteSpace(st.Url) && !string.IsNullOrWhiteSpace(st.Channel))
                    streams.Add(st);

        var referenced = streams.Select(st => st.Channel.Trim()).ToHashSet(StringComparer.Ordinal);

        var channels = new List<ExternalChannel>();
        await foreach (var c in FetchJsonItemsAsync<ExternalChannel>(
                           client, provider, provider.ChannelsEndpoint, cancellationToken))
            if (!string.IsNullOrWhiteSpace(c.Id) && referenced.Contains(c.Id.Trim()))
                channels.Add(c);

        // Logos: keep only the best candidate per referenced channel while streaming.
        var bestLogo = new Dictionary<string, ExternalLogo>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(provider.LogosEndpoint))
            await foreach (var l in FetchJsonItemsAsync<ExternalLogo>(
                               client, provider, provider.LogosEndpoint, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(l.Channel) || string.IsNullOrWhiteSpace(l.Logo))
                    continue;
                var ch = l.Channel.Trim();
                if (!referenced.Contains(ch))
                    continue;
                if (!bestLogo.TryGetValue(ch, out var current) || LogoRank(l).CompareTo(LogoRank(current)) > 0)
                    bestLogo[ch] = l;
            }

        List<ExternalFeed> feeds = [];
        if (!string.IsNullOrWhiteSpace(provider.FeedsEndpoint))
            await foreach (var f in FetchJsonItemsAsync<ExternalFeed>(
                               client, provider, provider.FeedsEndpoint, cancellationToken))
                if (!string.IsNullOrWhiteSpace(f.Channel) && referenced.Contains(f.Channel.Trim()))
                    feeds.Add(f);

        List<ExternalBlocklistEntry> blocklist = [];
        if (!string.IsNullOrWhiteSpace(provider.BlocklistEndpoint))
            await foreach (var e in FetchJsonItemsAsync<ExternalBlocklistEntry>(
                               client, provider, provider.BlocklistEndpoint, cancellationToken))
                blocklist.Add(e);

        return BuildResult(channels, streams, [.. bestLogo.Values], feeds, blocklist);
    }

    // Same preference as BuildResult: in_use, then no feed, then largest width.
    private static (int, int, int) LogoRank(ExternalLogo l)
        => (l.InUse ? 1 : 0, string.IsNullOrWhiteSpace(l.Feed) ? 1 : 0, l.Width);

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
