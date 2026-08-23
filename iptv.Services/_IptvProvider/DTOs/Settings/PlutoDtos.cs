using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Settings;

public class PlutoChannel
{
    [JsonPropertyName("_id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("logo")] public PlutoLogo Logo { get; set; }
    [JsonPropertyName("category")] public string Category { get; set; }
    [JsonPropertyName("stitched")] public PlutoStitched Stitched { get; set; }
}

public class PlutoLogo
{
    [JsonPropertyName("path")] public string Path { get; set; }
}

public class PlutoStitched
{
    [JsonPropertyName("urls")] public List<PlutoStreamUrl> Urls { get; set; } = [];
}

public class PlutoStreamUrl
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
}