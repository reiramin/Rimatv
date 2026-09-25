using System.Text.Json.Serialization;
using iptv.Services._Channel.DTOs.Results;

namespace iptv.Services._Stream.DTOs.Results;

/// <summary>
/// Response to Stream/ReportFailureAsync. Playback-like data for the replacement stream (§5.3).
/// When no alternative exists, <see cref="Found"/> is false and <see cref="ErrorCode"/> is
/// "USE_VPN" (with RequiredRegions and both messages) or "NoAlternativeStream" — the broken stream
/// is never returned. The replacement's playability fields come from <see cref="StreamOutputFields"/>.
/// </summary>
public class StreamReportFailureResult : StreamOutputFields
{
    public bool Found { get; set; }
    public string ErrorCode { get; set; }

    public string ChannelId { get; set; }
    public string CanonicalId { get; set; }

    public string StreamId { get; set; }
    public string StreamUri { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }

    public List<PlaybackFallback> Fallbacks { get; set; } = [];

    // USE_VPN details (§2.3).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string> RequiredRegions { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Message { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string MessageFa { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string VpnHelpUrl { get; set; }
}
