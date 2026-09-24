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

    // hls | youtube | resolve | direct | clientResolve | officialPlayer (see StreamTypes).
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

    // --- Playability metadata (set by sync) -------------------------------------------------

    // ISO country the stream only plays from (IR for Iranian CDNs / [IR] labels / registry
    // RequiresIranianIp, the source country for Geo-blocked labels, US for Pluto), else null.
    public string RequiredRegion { get; set; }

    // Reachable abroad but possibly filtered in Iran: RequiredRegion == null, Type == hls and a
    // non-Iranian host. Only a flag for a future, separately hosted relay.
    public bool RelayEligible { get; set; }

    // --- Deep-probe results (set by Stream/ReportProbeResultsAsync from GitHub Actions) ------

    // https, no required UA/Referer, and CORS allows https://rimatv.github.io. Null = not probed yet.
    public bool? WebCompatible { get; set; }

    // ok | dead | region | unverified (see StreamProbeStatus).
    public string ProbeStatus { get; set; }
    public DateTime? ProbeMoment { get; set; }
    public string ProbeRegionHint { get; set; }

    // --- Official-source ladder (resolver provider, type resolve / clientResolve / officialPlayer)

    public string PageUrl { get; set; }
    public string ResolveMethod { get; set; }
    public string ResolvePattern { get; set; }
    public string ResolveApiUrl { get; set; }
    public string ResolveBaseUrl { get; set; }
    public Dictionary<string, string> ResolveHeaders { get; set; }
    public int ResolveTtlSeconds { get; set; }
    public bool IpBound { get; set; }
    public string PlayerUrl { get; set; }
    public bool? Embeddable { get; set; }

    public string DataHash { get; set; }
}

public class ClientFailureReport
{
    public string UserPublicKey { get; set; }

    // Anonymous reports: the same client's key under the previous day's salt (see ReporterCounting).
    public string PreviousUserPublicKey { get; set; }

    public DateTime Moment { get; set; }
    public string Reason { get; set; }
}

public static class StreamTypes
{
    public const string Hls = "hls";
    public const string Direct = "direct";
    public const string YouTube = "youtube";
    public const string Resolve = "resolve";
    public const string ClientResolve = "clientResolve";
    public const string OfficialPlayer = "officialPlayer";

    public static bool IsStatic(string type) => type is null or Hls or Direct;
}

public static class StreamProbeStatus
{
    public const string Ok = "ok";
    public const string Dead = "dead";
    public const string Region = "region";
    public const string Unverified = "unverified";
}
