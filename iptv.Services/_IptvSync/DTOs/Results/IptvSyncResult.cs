namespace iptv.Services._IptvSync.DTOs.Results;

public class IptvSyncResult
{
    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }
    public string Status { get; set; }
    public string SyncLogPublicKey { get; set; }

    public int TotalChannels { get; set; }
    public int InsertedChannels { get; set; }
    public int UpdatedChannels { get; set; }
    public int DeactivatedChannels { get; set; }

    public int TotalStreams { get; set; }
    public int InsertedStreams { get; set; }
    public int UpdatedStreams { get; set; }
    public int DeactivatedStreams { get; set; }

    public string Message { get; set; }
}
