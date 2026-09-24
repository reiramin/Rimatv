namespace iptv.Services._Stream.Selection;

/// <summary>Client-failure thresholds, kept in one place (AGENT_PROMPT §5.3).</summary>
public static class StreamFailureConstants
{
    public const int DistinctUserThreshold = 3;
    public static readonly TimeSpan ReportWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DecayWindow = TimeSpan.FromMinutes(30);
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
}

public sealed class StreamSelectionContext
{
    public string CurrentStreamId { get; set; }
    public DateTime Now { get; set; } = DateTime.UtcNow;
    // Streams the client already tried this session.
    public HashSet<string> ExcludeStreamIds { get; set; } = new(StringComparer.Ordinal);
}
