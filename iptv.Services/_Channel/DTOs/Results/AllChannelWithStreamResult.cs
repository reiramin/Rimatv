using System.Text.Json.Serialization;

namespace iptv.Services._Channel.DTOs.Results;

public class FallbackStreamResult : StreamOutputFields
{
    public string StreamId { get; set; }
    public string Url { get; set; }
    public string UserAgent { get; set; }
    public string Referer { get; set; }
    public string Quality { get; set; }
    public string ProviderName { get; set; }
}

/// <summary>Winner stream fields (type, requiredRegion, …) are flattened via <see cref="StreamOutputFields"/>.</summary>
public class AllChannelWithStreamResult : StreamOutputFields
{
    public string ChannelId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }

    public string CurrentStreamUrl { get; set; }

    public string StreamUserAgent { get; set; }
    public string StreamReferer { get; set; }
    public string StreamQuality { get; set; }

    // Additive fields (§5.1).
    public string StreamId { get; set; }
    public string CanonicalId { get; set; }
    public string NameFa { get; set; }
    public string CuratedCountry { get; set; }
    public string ProviderName { get; set; }

    public List<FallbackStreamResult> FallbackStreams { get; set; } = [];

    // Ladder winner; CurrentStreamUrl and the flattened stream fields describe the first hls/direct
    // stream only (null when none).
    public FallbackStreamResult Playback { get; set; }

    // Channel-level playability (§2.2).
    public string Status { get; set; }
    public List<string> RequiredRegions { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ErrorCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Message { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string MessageFa { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string VpnHelpUrl { get; set; }
}
