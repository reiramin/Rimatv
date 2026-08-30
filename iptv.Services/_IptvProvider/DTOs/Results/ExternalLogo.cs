using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalLogo
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("url")] public string Logo { get; set; }
}