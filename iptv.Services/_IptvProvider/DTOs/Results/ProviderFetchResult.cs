namespace iptv.Services._IptvProvider.DTOs.Results;

/// <summary>
/// Uniform output of every <c>IProviderFetcher</c>. Carries channels, streams and logos in the
/// existing External* shapes so <c>IptvSyncService</c> can consume any provider identically.
/// </summary>
public class ProviderFetchResult
{
    public List<ExternalChannel> Channels { get; set; } = [];
    public List<ExternalStream> Streams { get; set; } = [];
    public List<ExternalLogo> Logos { get; set; } = [];

    // Ids the provider explicitly blocklisted (e.g. iptv-org blocklist.json), so sync can drop them.
    public HashSet<string> BlockedChannelIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
