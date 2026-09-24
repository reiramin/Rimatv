namespace iptv.Services._Stream.DTOs.Results;

/// <summary>
/// Response to Stream/ReportFailureAsync. Playback-like data for the replacement stream (§5.3).
/// When no alternative exists, <see cref="Found"/> is false and <see cref="ErrorCode"/> is
/// "NoAlternativeStream" — the broken stream is never returned.
/// </summary>
public class StreamReportFailureResult
{
    public bool Found { get; set; }
    public string ErrorCode { get; set; }

    public string ChannelId { get; set; }
    public string CanonicalId { get; set; }

    public string StreamId { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Type { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }

    public List<PlaybackFallback> Fallbacks { get; set; } = [];
}
