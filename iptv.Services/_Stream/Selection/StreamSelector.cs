using Utilities.Constants;

namespace iptv.Services._Stream.Selection;

/// <summary>
/// Single shared, deterministic stream-selection policy (AGENT_PROMPT §6). Pure — no I/O — so it is
/// exhaustively unit tested. Used by GetAllUnpagedWithStream, GetCuratedListWithStream, ReportFailure
/// and GetPlaybackStream.
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
            .OrderBy(c => c.RecentReportCount)                       // 1. fewer recent client reports first
            .ThenBy(c => IsHttps(c.StreamUri) ? 0 : 1)               // 2. https before http
            .ThenBy(c => NeedsHeaders(c) ? 1 : 0)                    // 3. no required headers first
            .ThenByDescending(c => c.ProviderPriority)               // 4. provider priority
            .ThenBy(c => c.IsAdaptive ? 0 : 1)                       // 5a. adaptive master first
            .ThenBy(c => c.QualityRank <= 1080 ? 0 : 1)              // 5b. <=1080p before 1440p/2160p
            .ThenByDescending(c => c.QualityRank)                    // 5c. then highest rank
            .ThenBy(c => c.StreamId == context.CurrentStreamId ? 0 : 1) // 6a. sticky current stream
            .ThenBy(c => c.StreamId, StringComparer.Ordinal)        // 6b. deterministic tiebreak
            .ToList();
    }

    private static bool IsEligible(StreamCandidate c, StreamSelectionContext context, DateTime now)
    {
        if (c.Inactive || c.AdminDisabled)
            return false;

        if (context.ExcludeStreamIds.Contains(c.StreamId))
            return false;

        // Client-failing streams decay out naturally.
        if (c.ClientFailingUntil.HasValue && now < c.ClientFailingUntil.Value)
            return false;

        // For streams the abroad health checker cannot judge, ignore IsHealthy and rely on client
        // reports; for everything else require IsHealthy.
        if (!c.ServerProbeUnreliable && !c.IsHealthy)
            return false;

        return true;
    }

    private static bool IsHttps(string url)
        => !string.IsNullOrEmpty(url) && url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsHeaders(StreamCandidate c)
        => !string.IsNullOrWhiteSpace(c.UserAgent) || !string.IsNullOrWhiteSpace(c.Referer);
}
