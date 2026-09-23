using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalLogo
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("feed")] public string Feed { get; set; }
    [JsonPropertyName("url")] public string Logo { get; set; }

    // logos.json additions (2025-07 onwards): prefer in_use=true, no feed, largest width.
    [JsonPropertyName("in_use")] public bool InUse { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
}
