using System.Text.Json.Serialization;

namespace iptv.Services._Channel.DTOs.Results;

/// <summary>
/// Per-stream playability fields shared by the winner (flattened onto the channel) and every
/// fallback stream. All are omitted when null to keep the payload small.
/// </summary>
public class StreamOutputFields
{
    // hls | youtube | resolve | direct | clientResolve | officialPlayer
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Type { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string RequiredRegion { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? WebCompatible { get; set; }

    // type == resolve: call this first; it returns the short-lived .m3u8.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ResolveUrl { get; set; }

    // type == clientResolve: the app fetches pageUrl (or apiUrl) itself and extracts the .m3u8.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string PageUrl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Method { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Pattern { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string ApiUrl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, string> Headers { get; set; }

    // type == officialPlayer: open in an iframe/WebView when embeddable, else a new tab/browser.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string PlayerUrl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Embeddable { get; set; }

    // Only when a relay is configured and the stream is RelayEligible (§6).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string RelayUrl { get; set; }
}
