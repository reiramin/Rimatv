using System.Text.Json.Serialization;
using iptv.Domain.Collections;
using iptv.Services._IptvProvider.DTOs.Results;
using Utilities.Constants;

namespace iptv.Services._IptvProvider.Fetchers;

public class FamelackChannel
{
    [JsonPropertyName("nanoid")] public string NanoId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("sources")] public FamelackSources Sources { get; set; }
    [JsonPropertyName("languages")] public List<string> Languages { get; set; } = [];
    [JsonPropertyName("country")] public string Country { get; set; }
    [JsonPropertyName("isGeoBlocked")] public bool IsGeoBlocked { get; set; }
}

public class FamelackSources
{
    [JsonPropertyName("streams")] public List<string> Streams { get; set; } = [];
}

/// <summary>
/// famelack/famelack-channels — per-country JSON files. One provider aggregates several endpoints
/// (ChannelsEndpoint + AdditionalEndpoints, one per country key). Each entry has multiple streams.
/// </summary>
public class FamelackProviderFetcher(IHttpClientFactory httpClientFactory)
    : ProviderFetcherBase(httpClientFactory), IProviderFetcher, RegisterMode.IScopedDependency
{
    public ProviderKind Kind => ProviderKind.FamelackJson;

    public async Task<ProviderFetchResult> FetchAsync(
        IptvProviders provider, CancellationToken cancellationToken)
    {
        using var client = CreateClient(provider);

        var endpoints = new List<string>();
        if (!string.IsNullOrWhiteSpace(provider.ChannelsEndpoint))
            endpoints.Add(provider.ChannelsEndpoint);
        endpoints.AddRange(provider.AdditionalEndpoints.Where(e => !string.IsNullOrWhiteSpace(e)));

        var all = new List<FamelackChannel>();
        foreach (var endpoint in endpoints)
        {
            try
            {
                var batch = await FetchJsonAsync<List<FamelackChannel>>(
                    client, provider, endpoint, cancellationToken) ?? [];
                all.AddRange(batch);
            }
            catch
            {
                // A single missing country file must not abort the whole provider.
            }
        }

        return BuildResult(all);
    }

    internal static ProviderFetchResult BuildResult(List<FamelackChannel> channels)
    {
        var result = new ProviderFetchResult();
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var c in channels)
        {
            if (string.IsNullOrWhiteSpace(c.NanoId) || string.IsNullOrWhiteSpace(c.Name))
                continue;

            var urls = c.Sources?.Streams?
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            if (urls.Count == 0)
                continue;

            var id = c.NanoId.Trim();
            if (emitted.Add(id))
            {
                result.Channels.Add(new ExternalChannel
                {
                    Id = id,
                    Name = c.Name.Trim(),
                    Country = c.Country?.Trim().ToUpperInvariant(),
                    Categories = [],
                    Languages = c.Languages ?? [],
                    Labels = c.IsGeoBlocked ? ["Geo-blocked"] : []
                });
            }

            foreach (var url in urls)
                result.Streams.Add(new ExternalStream
                {
                    Channel = id,
                    Url = url,
                    Languages = c.Languages ?? [],
                    GeoBlocked = c.IsGeoBlocked,
                    Labels = c.IsGeoBlocked ? ["Geo-blocked"] : []
                });
        }

        return result;
    }
}
