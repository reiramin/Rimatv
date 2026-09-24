using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using Utilities.Constants;

namespace iptv.Services._IptvProvider.Fetchers;

public class M3uProviderFetcher(IHttpClientFactory httpClientFactory)
    : ProviderFetcherBase(httpClientFactory), IProviderFetcher, RegisterMode.IScopedDependency
{
    public ProviderKind Kind => ProviderKind.M3u;

    public async Task<ProviderFetchResult> FetchAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        using var client = CreateClient(provider);

        var endpoints = new List<string>();
        if (!string.IsNullOrWhiteSpace(provider.ChannelsEndpoint))
            endpoints.Add(provider.ChannelsEndpoint);
        endpoints.AddRange(provider.AdditionalEndpoints.Where(e => !string.IsNullOrWhiteSpace(e)));

        var entries = new List<M3uEntry>();

        foreach (var endpoint in endpoints)
        {
            var lines = new List<string>();
            await foreach (var line in FetchLinesAsync(client, provider, endpoint, cancellationToken))
                lines.Add(line);

            entries.AddRange(M3uPlaylistParser.Parse(lines));
        }

        return BuildResult(entries);
    }

    internal static ProviderFetchResult BuildResult(List<M3uEntry> entries)
    {
        var result = new ProviderFetchResult();

        var channelsById = new Dictionary<string, ExternalChannel>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ChannelId) || string.IsNullOrWhiteSpace(entry.Url))
                continue;

            if (!channelsById.ContainsKey(entry.ChannelId))
            {
                channelsById[entry.ChannelId] = new ExternalChannel
                {
                    Id = entry.ChannelId,
                    Name = entry.Name,
                    Country = entry.Country,
                    Logo = entry.Logo,
                    Categories = string.IsNullOrWhiteSpace(entry.GroupTitle) ? [] : [entry.GroupTitle],
                    CanonicalIdHint = entry.CanonicalIdHint,
                    Feed = entry.Feed,
                    Labels = [.. entry.Labels],
                    Languages = string.IsNullOrWhiteSpace(entry.Language) ? [] : [entry.Language],
                    TvgId = entry.CanonicalIdHint
                };
            }

            result.Streams.Add(new ExternalStream
            {
                Channel = entry.ChannelId,
                Feed = entry.Feed,
                Url = entry.Url,
                UserAgent = entry.UserAgent,
                Referrer = entry.Referer,
                Quality = entry.Quality,
                Labels = [.. entry.Labels],
                Languages = string.IsNullOrWhiteSpace(entry.Language) ? [] : [entry.Language],
                RequiresIranianIp = entry.RequiresIranianIp,
                GeoBlocked = entry.GeoBlocked
            });
        }

        result.Channels = [.. channelsById.Values];
        return result;
    }
}
