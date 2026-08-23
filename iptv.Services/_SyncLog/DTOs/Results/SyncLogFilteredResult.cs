namespace iptv.Services._SyncLog.DTOs.Results;

public class SyncLogFilteredResult
{
    public string PublicKey { get; set; }
    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }
    public string Status { get; set; }

    public int TotalChannels { get; set; }
    public int InsertedChannels { get; set; }
    public int UpdatedChannels { get; set; }
    public int DeactivatedChannels { get; set; }

    public int TotalStreams { get; set; }
    public int InsertedStreams { get; set; }
    public int UpdatedStreams { get; set; }
    public int DeactivatedStreams { get; set; }

    public string Message { get; set; }

    public DateTime StartMoment { get; set; }
    public DateTime? EndMoment { get; set; }
}
