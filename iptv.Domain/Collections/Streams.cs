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

    // Adaptive master playlist (a.k.a. "Auto"): a separate, preferred class in selection.
    public bool IsAdaptive { get; set; }

    public string Feed { get; set; }
    public List<string> Languages { get; set; } = [];

    public bool IsHealthy { get; set; }
    public bool Inactive { get; set; }

    // Set by the admin Activate endpoint; sync never clears it.
    public bool AdminDisabled { get; set; }

    // When true, the abroad health-checker cannot judge this stream (Iranian-only CDN,
    // [IR]/Geo-blocked label, registry RequiresIranianIp). Selection ignores IsHealthy for it.
    public bool ServerProbeUnreliable { get; set; }

    // Client failure reporting (see StreamService.ReportStreamFailureAsync).
    public int ClientFailureCount { get; set; }
    public DateTime? LastClientFailureMoment { get; set; }
    public List<ClientFailureReport> RecentClientFailures { get; set; } = [];

    // Set when >=3 distinct users reported within the window; excludes the stream from selection
    // until this moment (natural decay, no background job).
    public DateTime? ClientFailingUntil { get; set; }

    public DateTime? LastCheckedMoment { get; set; }

    public string DataHash { get; set; }
}

public class ClientFailureReport
{
    public string UserPublicKey { get; set; }
    public DateTime Moment { get; set; }
    public string Reason { get; set; }
}
