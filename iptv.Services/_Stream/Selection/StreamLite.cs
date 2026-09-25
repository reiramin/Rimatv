using System.Linq.Expressions;
using iptv.Domain.Collections;

namespace iptv.Services._Stream.Selection;

/// <summary>
/// Slim stream shape for the lite cache and selection: only the fields selection and the outputs
/// need (no Name / Feed / Languages / DataHash / ExternalId / audit fields), so the cached list stays
/// small on the 512 MB instance. <see cref="Projection"/> is translated server-side by MongoDB LINQ3
/// into a $project (verified by an integration test against local Mongo).
/// </summary>
internal sealed class StreamLite
{
    public string StreamId { get; set; }
    public string ChannelId { get; set; }
    public string ProviderPublicKey { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public int QualityRank { get; set; }
    public bool IsAdaptive { get; set; }

    // Not projected by the lite cache (its query already filters them out); set by FromDoc.
    public bool Inactive { get; set; }
    public bool AdminDisabled { get; set; }

    public bool IsHealthy { get; set; }
    public bool ServerProbeUnreliable { get; set; }
    public DateTime? ClientFailingUntil { get; set; }
    public List<ClientFailureReport> RecentClientFailures { get; set; }

    public string Type { get; set; }
    public string RequiredRegion { get; set; }
    public bool RelayEligible { get; set; }
    public bool? WebCompatible { get; set; }
    public string ProbeStatus { get; set; }
    public DateTime? ProbeMoment { get; set; }

    public string PageUrl { get; set; }
    public string ResolveMethod { get; set; }
    public string ResolvePattern { get; set; }
    public string ResolveApiUrl { get; set; }
    public string ResolveBaseUrl { get; set; }
    public Dictionary<string, string> ResolveHeaders { get; set; }
    public string PlayerUrl { get; set; }
    public bool? Embeddable { get; set; }

    public static readonly Expression<Func<Streams, StreamLite>> Projection = q => new StreamLite
    {
        StreamId = q.StreamId,
        ChannelId = q.ChannelId,
        ProviderPublicKey = q.ProviderPublicKey,
        StreamUri = q.StreamUri,
        UserAgent = q.UserAgent,
        Referer = q.Referer,
        Quality = q.Quality,
        QualityRank = q.QualityRank,
        IsAdaptive = q.IsAdaptive,
        IsHealthy = q.IsHealthy,
        ServerProbeUnreliable = q.ServerProbeUnreliable,
        ClientFailingUntil = q.ClientFailingUntil,
        RecentClientFailures = q.RecentClientFailures,
        Type = q.Type,
        RequiredRegion = q.RequiredRegion,
        RelayEligible = q.RelayEligible,
        WebCompatible = q.WebCompatible,
        ProbeStatus = q.ProbeStatus,
        ProbeMoment = q.ProbeMoment,
        PageUrl = q.PageUrl,
        ResolveMethod = q.ResolveMethod,
        ResolvePattern = q.ResolvePattern,
        ResolveApiUrl = q.ResolveApiUrl,
        ResolveBaseUrl = q.ResolveBaseUrl,
        ResolveHeaders = q.ResolveHeaders,
        PlayerUrl = q.PlayerUrl,
        Embeddable = q.Embeddable
    };

    private static readonly Func<Streams, StreamLite> Compiled = Projection.Compile();

    public static StreamLite FromDoc(Streams s)
    {
        var lite = Compiled(s);
        lite.Inactive = s.Inactive;
        lite.AdminDisabled = s.AdminDisabled;
        return lite;
    }
}
