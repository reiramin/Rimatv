using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

[MonjoCollectionName("SyncLogs")]
public class SyncLogs : BaseDocument
{
    public string PublicKey { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }

    public SyncStatus Status { get; set; } = SyncStatus.Running;

    public int TotalChannels { get; set; }
    public int InsertedChannels { get; set; }
    public int UpdatedChannels { get; set; }
    public int DeactivatedChannels { get; set; }

    public int TotalStreams { get; set; }
    public int InsertedStreams { get; set; }
    public int UpdatedStreams { get; set; }
    public int DeactivatedStreams { get; set; }

    public string Message { get; set; }

    public DateTime StartMoment { get; set; } = DateTime.UtcNow;
    public DateTime? EndMoment { get; set; }
}

public enum SyncStatus { Running, Success, PartialSuccess, Failed }
