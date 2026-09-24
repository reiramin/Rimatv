using iptv.Services._Stream.Reporting;
using iptv.Domain.Collections;

namespace iptv.Services._Stream.Selection;

/// <summary>Name and priority of an ACTIVE provider, keyed by provider public key.</summary>
public readonly record struct ProviderInfo(string Name, int Priority);

/// <summary>Builds <see cref="StreamCandidate"/>s from persisted streams and computes the derived
/// client-failure signals the selector needs.</summary>
public static class StreamCandidateFactory
{
    public static int RecentReportCount(IEnumerable<ClientFailureReport> reports, DateTime now)
    {
        if (reports == null) return 0;
        var since = now - StreamFailureConstants.ReportWindow;
        return ReporterCounting.Distinct(reports.Where(r => r.Moment >= since));
    }

    /// <summary>
    /// Builds candidates for streams whose provider is in <paramref name="activeProviders"/>.
    /// A stream whose provider is inactive or deleted is skipped — never offered with priority 0.
    /// </summary>
    public static List<StreamCandidate> FromActiveProviders(
        IEnumerable<Streams> streams,
        Func<Streams, string> canonicalOf,
        IReadOnlyDictionary<string, ProviderInfo> activeProviders,
        DateTime now)
    {
        var result = new List<StreamCandidate>();
        foreach (var s in streams)
        {
            if (string.IsNullOrEmpty(s.ProviderPublicKey) ||
                !activeProviders.TryGetValue(s.ProviderPublicKey, out var provider))
                continue;

            result.Add(FromDoc(s, canonicalOf(s), provider.Priority, provider.Name, now));
        }

        return result;
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
            ClientFailingUntil = s.ClientFailingUntil,
            Type = string.IsNullOrWhiteSpace(s.Type) ? InferType(s.StreamUri) : s.Type,
            RequiredRegion = string.IsNullOrWhiteSpace(s.RequiredRegion) ? null : s.RequiredRegion,
            WebCompatible = s.WebCompatible,
            ProbeStatus = s.ProbeStatus,
            ProbeMoment = s.ProbeMoment,
            RelayEligible = s.RelayEligible,
            PageUrl = s.PageUrl,
            ResolveMethod = s.ResolveMethod,
            ResolvePattern = s.ResolvePattern,
            ResolveApiUrl = s.ResolveApiUrl,
            ResolveBaseUrl = s.ResolveBaseUrl,
            ResolveHeaders = s.ResolveHeaders is { Count: > 0 } ? s.ResolveHeaders : null,
            PlayerUrl = s.PlayerUrl,
            Embeddable = s.Embeddable
        };

    // Streams synced before Type was always set.
    private static string InferType(string uri)
        => uri != null && uri.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? StreamTypes.Hls : StreamTypes.Direct;
}
