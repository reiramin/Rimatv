using iptv.Domain.Collections;

namespace iptv.Services._Stream.Selection;

/// <summary>Builds <see cref="StreamCandidate"/>s from persisted streams and computes the derived
/// client-failure signals the selector needs.</summary>
public static class StreamCandidateFactory
{
    public static int RecentReportCount(IEnumerable<ClientFailureReport> reports, DateTime now)
    {
        if (reports == null) return 0;
        var since = now - StreamFailureConstants.ReportWindow;
        return reports
            .Where(r => r.Moment >= since && !string.IsNullOrEmpty(r.UserPublicKey))
            .Select(r => r.UserPublicKey)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    public static StreamCandidate FromDoc(
        Streams s, string canonicalId, int providerPriority, string providerName, DateTime now)
        => new()
        {
            StreamId = s.StreamId,
            ChannelId = s.ChannelId,
            CanonicalId = canonicalId,
            ProviderPublicKey = s.ProviderPublicKey,
            ProviderName = providerName,
            ProviderPriority = providerPriority,
            StreamUri = s.StreamUri,
            UserAgent = string.IsNullOrWhiteSpace(s.UserAgent) ? null : s.UserAgent,
            Referer = string.IsNullOrWhiteSpace(s.Referer) ? null : s.Referer,
            Quality = s.Quality,
            QualityRank = s.QualityRank,
            IsAdaptive = s.IsAdaptive,
            Inactive = s.Inactive,
            AdminDisabled = s.AdminDisabled,
            IsHealthy = s.IsHealthy,
            ServerProbeUnreliable = s.ServerProbeUnreliable,
            RecentReportCount = RecentReportCount(s.RecentClientFailures, now),
            ClientFailingUntil = s.ClientFailingUntil
        };
}
