namespace iptv.Services._Stream.DTOs.Results;

public class StreamFilteredResult
{
    public string StreamId { get; set; }
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string StreamUri { get; set; }
    public string Type { get; set; }
    public string? Quality { get; set; }
    public bool IsHealthy { get; set; }
    public bool Inactive { get; set; }
    public bool AdminDisabled { get; set; }
    public DateTime? LastCheckedMoment { get; set; }
}

// Admin view: every stream of a channel, with the probe and playability metadata.
public class StreamAdminResult : StreamFilteredResult
{
    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }
    public string ProbeStatus { get; set; }
    public DateTime? ProbeMoment { get; set; }
    public string RequiredRegion { get; set; }
    public bool? WebCompatible { get; set; }
}
