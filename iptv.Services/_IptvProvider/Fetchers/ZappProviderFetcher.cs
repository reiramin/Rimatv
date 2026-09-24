using System.Text.Json.Serialization;
using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using Utilities.Constants;

namespace iptv.Services._IptvProvider.Fetchers;

public class ZappChannel
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("streamUrl")] public string StreamUrl { get; set; }
}

/// <summary>
/// MediathekView Zapp API — channelInfoList is a dictionary keyed by channel slug. Country DE.
/// German public-broadcaster CDNs geo-block their /de/ variants; where a /de/ HLS variant exists we
/// also emit the /int/ variant first (playable from abroad).
/// </summary>
public class ZappProviderFetcher(IHttpClientFactory httpClientFactory)
    : ProviderFetcherBase(httpClientFactory), IProviderFetcher, RegisterMode.IScopedDependency
{
    public ProviderKind Kind => ProviderKind.ZappJson;

    public async Task<ProviderFetchResult> FetchAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        using var client = CreateClient(provider);

        var map = await FetchJsonAsync<Dictionary<string, ZappChannel>>(
            client, provider, provider.ChannelsEndpoint, cancellationToken) ?? new();

        return BuildResult(map);
    }

    internal static ProviderFetchResult BuildResult(Dictionary<string, ZappChannel> map)
    {
        var result = new ProviderFetchResult();

        foreach (var (slug, info) in map)
        {
            if (string.IsNullOrWhiteSpace(slug) || info == null ||
                string.IsNullOrWhiteSpace(info.StreamUrl) || string.IsNullOrWhiteSpace(info.Name))
                continue;

            var id = slug.Trim();

            result.Channels.Add(new ExternalChannel
            {
                Id = id,
                Name = info.Name.Trim(),
                Country = "DE",
                Categories = [],
                Languages = ["deu"]
            });

            var deUrl = info.StreamUrl.Trim();

            // Prefer an /int/ (non-geo-blocked) variant when the URL is the /de/ variant.
            if (deUrl.Contains("/de/", StringComparison.OrdinalIgnoreCase))
            {
                var intUrl = deUrl.Replace("/de/", "/int/", StringComparison.OrdinalIgnoreCase);
                result.Streams.Add(new ExternalStream
                {
                    Channel = id, Url = intUrl, Languages = ["deu"]
                });
            }

            result.Streams.Add(new ExternalStream
            {
                Channel = id,
                Url = deUrl,
                Languages = ["deu"],
                GeoBlocked = deUrl.Contains("/de/", StringComparison.OrdinalIgnoreCase),
                Labels = deUrl.Contains("/de/", StringComparison.OrdinalIgnoreCase) ? ["Geo-blocked"] : []
            });
        }

        return result;
    }
}
