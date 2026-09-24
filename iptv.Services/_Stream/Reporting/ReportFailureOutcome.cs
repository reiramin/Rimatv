using iptv.Domain.Collections;
using iptv.Services._Channel.Playability;
using iptv.Services._Stream.DTOs.Updates;
using iptv.Services._Stream.Selection;

namespace iptv.Services._Stream.Reporting;

/// <summary>
/// Decides how ReportFailureAsync answers when no alternative stream remains (§2.3).
/// USE_VPN when any of:
///   (a) the reported stream or any excluded stream has a RequiredRegion;
///   (b) every excluded stream had an ok deep probe within 90 min — they are alive on the internet,
///       so the user's network is blocking them (typical for Iran without a VPN);
///   (c) the reason is Http4xx on a stream that has a region.
/// Otherwise NoAlternativeStream.
/// </summary>
public static class ReportFailureOutcome
{
    public const string NoAlternativeStream = "NoAlternativeStream";

    public sealed record Decision(bool UseVpn, List<string> RequiredRegions);

    /// <summary>
    /// The client's excludeStreamIds, scoped to the reported canonical channel: ids of other
    /// channels are ignored (they must not influence rules (a)/(b)). The reported stream is always
    /// included.
    /// </summary>
    public static List<Streams> ScopeExcluded(
        IEnumerable<string> requestedIds, IReadOnlyCollection<Streams> channelStreams, Streams reported)
    {
        var requested = new HashSet<string>(requestedIds ?? [], StringComparer.Ordinal);
        var scoped = (channelStreams ?? [])
            .Where(s => s.StreamId != reported?.StreamId && requested.Contains(s.StreamId))
            .ToList();
        if (reported != null)
            scoped.Add(reported);
        return scoped;
    }

    /// <param name="excluded">All streams the client has tried, including the reported one.</param>
    public static Decision Decide(
        Streams reported, IReadOnlyCollection<Streams> excluded, StreamFailureReason reason, DateTime now)
    {
        var all = new List<Streams>(excluded ?? []);
        if (reported != null && all.All(s => s.StreamId != reported.StreamId))
            all.Add(reported);

        var regions = ChannelStatusRules.DistinctRegions(all.Select(s => s.RequiredRegion));

        var a = regions.Count > 0;
        var b = all.Count > 0 && all.All(s =>
            s.ProbeStatus == StreamProbeStatus.Ok && s.ProbeMoment.HasValue &&
            now - s.ProbeMoment.Value < ProbeWindows.ClientFailureSuppression);
        var c = reason == StreamFailureReason.Http4xx && !string.IsNullOrWhiteSpace(reported?.RequiredRegion);

        return new Decision(a || b || c, regions);
    }
}
