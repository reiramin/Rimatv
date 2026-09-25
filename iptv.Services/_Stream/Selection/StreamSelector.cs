using iptv.Domain.Collections;
using Utilities.Constants;

namespace iptv.Services._Stream.Selection;

/// <summary>
/// Single shared, deterministic stream-selection policy. Pure — no I/O — so it is exhaustively
/// unit tested. Used by GetAllUnpagedWithStream, GetCuratedListWithStream, ReportFailure and
/// GetPlaybackStream.
/// </summary>
public class StreamSelector : IStreamSelector, RegisterMode.ISingletonDependency
{
    public IReadOnlyList<StreamCandidate> Order(
        IEnumerable<StreamCandidate> candidates, StreamSelectionContext context)
    {
        context ??= new StreamSelectionContext();
        var now = context.Now == default ? DateTime.UtcNow : context.Now;

        var eligible = candidates.Where(c => IsEligible(c, context, now));

        return eligible
            .OrderBy(c => ProbeClass(c, context, now))                // 1. probe: ok → unverified → region
            .ThenBy(c => RegionRank(c, context))                      // 2. iran: IR first; others: regional last
            .ThenBy(c => PlayedRank(c, now))                          // 2a. actually played (fresh ok) first
            .ThenBy(c => TypeRank(c.Type))                            // 2b. static → resolve → youtube → clientResolve → officialPlayer
            .ThenBy(c => WebCompatibleRank(c.WebCompatible))          // 3. web-compatible first
            .ThenBy(c => c.RecentReportCount)                         // 4. fewer recent client reports first
            .ThenBy(c => IsHttps(c.StreamUri) ? 0 : 1)                //    https before http
            .ThenBy(c => NeedsHeaders(c) ? 1 : 0)                     //    no required headers first
            .ThenByDescending(c => c.ProviderPriority)                //    provider priority
            .ThenBy(c => c.IsAdaptive ? 0 : 1)                        //    adaptive master first
            .ThenBy(c => c.QualityRank <= 1080 ? 0 : 1)               //    <=1080p before 1440p/2160p
            .ThenByDescending(c => c.QualityRank)                     //    then highest rank
            .ThenBy(c => c.StreamId == context.CurrentStreamId ? 0 : 1) // sticky current stream
            .ThenBy(c => c.StreamId, StringComparer.Ordinal)         //    deterministic tiebreak
            .ToList();
    }

    internal static bool IsEligible(StreamCandidate c, StreamSelectionContext context, DateTime now)
    {
        if (c.Inactive || c.AdminDisabled)
            return false;

        if (context.ExcludeStreamIds.Contains(c.StreamId))
            return false;

        // A fresh deep probe overrides the HEAD-only health flag in both directions.
        if (IsProbe(c, StreamProbeStatus.Dead, ProbeWindows.DeepProbe, now))
            return false;

        // Client-failing streams decay out naturally — unless a recent deep probe says the stream
        // is alive: then the reporters' own network is blocking it (typically Iran without a VPN)
        // and excluding it would also take it away from VPN users. Region-locked streams are never
        // excluded by reports: the probe (abroad) can never mark them ok, so a few diaspora/VPN
        // users could otherwise knock IR streams out for everyone in Iran. Reports are still recorded.
        if (c.RequiredRegion == null &&
            c.ClientFailingUntil.HasValue && now < c.ClientFailingUntil.Value &&
            !IsProbe(c, StreamProbeStatus.Ok, ProbeWindows.ClientFailureSuppression, now))
            return false;

        // The HEAD-only checker wrongly marks many servers that reject HEAD as unhealthy.
        if (IsProbe(c, StreamProbeStatus.Ok, ProbeWindows.DeepProbe, now))
            return true;

        // For streams the abroad health checker cannot judge, ignore IsHealthy and rely on client
        // reports; for everything else require IsHealthy.
        if (!c.ServerProbeUnreliable && !c.IsHealthy)
            return false;

        return true;
    }

    /// <summary>
    /// 0 = fresh ok, 1 = unverified (no/stale probe), 2 = region. On an <c>iran</c> channel an
    /// <c>IR</c> stream probed as <c>region</c> counts as 0: the probe runs abroad, where
    /// <c>region</c> is the best possible verdict for a stream that plays inside Iran.
    /// </summary>
    internal static int ProbeClass(StreamCandidate c, StreamSelectionContext context, DateTime now)
    {
        if (IsProbe(c, StreamProbeStatus.Ok, ProbeWindows.DeepProbe, now))
            return 0;

        if (c.ProbeStatus == StreamProbeStatus.Region)
            return context.IsIranChannel && c.RequiredRegion == "IR" ? 0 : 2;

        return 1;
    }

    internal static int TypeRank(string type) => type switch
    {
        StreamTypes.Resolve => 1,
        StreamTypes.YouTube => 2,
        StreamTypes.ClientResolve => 3,
        StreamTypes.OfficialPlayer => 4,
        _ => 0 // hls / direct
    };

    /// <summary>
    /// 0 = a fresh ok probe (the stream actually played), else 1. Only splits probe class 0 on an
    /// <c>iran</c> channel: an IR stream that played beats one the abroad probe could only call region.
    /// </summary>
    internal static int PlayedRank(StreamCandidate c, DateTime now)
        => IsProbe(c, StreamProbeStatus.Ok, ProbeWindows.DeepProbe, now) ? 0 : 1;

    private static int RegionRank(StreamCandidate c, StreamSelectionContext context)
    {
        if (context.IsIranChannel)
            return c.RequiredRegion == "IR" ? 0 : 1;   // reachable without VPN inside Iran

        return c.RequiredRegion == null ? 0 : 1;
    }

    private static int WebCompatibleRank(bool? webCompatible) => webCompatible switch
    {
        true => 0,
        null => 1,
        false => 2
    };

    private static bool IsProbe(StreamCandidate c, string status, TimeSpan window, DateTime now)
        => c.ProbeStatus == status && c.ProbeMoment.HasValue && now - c.ProbeMoment.Value < window;

    private static bool IsHttps(string url)
        => !string.IsNullOrEmpty(url) && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsHeaders(StreamCandidate c)
        => !string.IsNullOrWhiteSpace(c.UserAgent) || !string.IsNullOrWhiteSpace(c.Referer);
}
