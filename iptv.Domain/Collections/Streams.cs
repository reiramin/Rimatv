using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

[MonjoCollectionName("Streams")]
public class Streams : BaseDocument
{
    public string StreamId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderPublicKey { get; set; }
    public string ChannelId { get; set; }
    public string ExternalId { get; set; }

    public string Name { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }

    public string Type { get; set; }
    public string? Quality { get; set; }
    
    public int QualityRank { get; set; }   // مثلاً ارتفاع رزولوشن به پیکسل: 2160, 1080, 720, 480 ...

    public bool IsHealthy { get; set; }
    public bool Inactive { get; set; }

    public DateTime? LastCheckedMoment { get; set; }

    public string DataHash { get; set; }
}