namespace iptv.Services._IptvNotifier.DTOs.Results;

public class ChannelChangedEvent
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string ChangeType { get; set; }
}

public class StreamHealthChangedEvent
{
    public string StreamId { get; set; }
    public string ChannelId { get; set; }
    public bool IsHealthy { get; set; }
    public string ReplacementStreamId { get; set; }
    public string ReplacementStreamUri { get; set; }
}

public class SyncCompletedEvent
{
    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }
    public string Status { get; set; }
    public int InsertedChannels { get; set; }
    public int UpdatedChannels { get; set; }
    public int DeactivatedChannels { get; set; }
    public int InsertedStreams { get; set; }
    public int UpdatedStreams { get; set; }
    public int DeactivatedStreams { get; set; }
}
