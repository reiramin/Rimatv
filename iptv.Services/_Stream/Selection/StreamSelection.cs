namespace iptv.Services._Stream.Selection;

/// <summary>Client-failure thresholds, kept in one place (AGENT_PROMPT §5.3).</summary>
public static class StreamFailureConstants
{
    public const int DistinctUserThreshold = 3;
    public static readonly TimeSpan ReportWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DecayWindow = TimeSpan.FromMinutes(30);
}

/// <summary>Freshness windows for deep-probe results reported from GitHub Actions.</summary>
public static class ProbeWindows
{
    // A fresh "ok" makes a stream eligible even when the HEAD-only checker says unhealthy;
    // a fresh "dead" makes it ineligible.
    public static readonly TimeSpan DeepProbe = TimeSpan.FromHours(3);

    // Client-failure exclusion is suppressed while the stream has an "ok" probe this recent:
    // the stream is alive, so the reporters' network (e.g. Iran without VPN) is blocking it.
    public static readonly TimeSpan ClientFailureSuppression = TimeSpan.FromMinutes(90);
}

/// <summary>A single stream considered for playback selection. Deliberately POCO so the selector is pure.</summary>
public sealed class StreamCandidate
{
    public string StreamId { get; set; }
    public string ChannelId { get; set; }
    public string CanonicalId { get; set; }

    public string ProviderPublicKey { get; set; }
    public string ProviderName { get; set; }
    public int ProviderPriority { get; set; }

    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public int QualityRank { get; set; }
    public bool IsAdaptive { get; set; }

    public bool Inactive { get; set; }
    public bool AdminDisabled { get; set; }
    public bool IsHealthy { get; set; }
    public bool ServerProbeUnreliable { get; set; }

    // Distinct reporters in the last ReportWindow (used only for ordering).
    public int RecentReportCount { get; set; }
    // Excluded from selection while now < ClientFailingUntil.
    public DateTime? ClientFailingUntil { get; set; }

    // Playability metadata (§2).
    public string Type { get; set; }
    public string RequiredRegion { get; set; }
    public bool? WebCompatible { get; set; }
    public string ProbeStatus { get; set; }
    public DateTime? ProbeMoment { get; set; }
    public bool RelayEligible { get; set; }

    // Official-source ladder data, passed through to the outputs (§3).
    public string PageUrl { get; set; }
    public string ResolveMethod { get; set; }
    public string ResolvePattern { get; set; }
    public string ResolveApiUrl { get; set; }
    public string ResolveBaseUrl { get; set; }
    public Dictionary<string, string> ResolveHeaders { get; set; }
    public string PlayerUrl { get; set; }
    public bool? Embeddable { get; set; }
}

public sealed class StreamSelectionContext
{
    public string CurrentStreamId { get; set; }
    public DateTime Now { get; set; } = DateTime.UtcNow;
    // Streams the client already tried this session.
    public HashSet<string> ExcludeStreamIds { get; set; } = new(StringComparer.Ordinal);

    // Registry curated key of the channel ("iran", "iran-foreign", "tr", ...); drives IR-first.
    public string CuratedCountry { get; set; }

    public bool IsIranChannel => string.Equals(CuratedCountry, "iran", StringComparison.OrdinalIgnoreCase);
}
