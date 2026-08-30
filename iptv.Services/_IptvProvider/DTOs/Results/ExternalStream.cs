using System.Text.Json.Serialization;


namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalStream
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("user_agent")] public string UserAgent { get; set; }
    [JsonPropertyName("referrer")] public string Referrer { get; set; }
    [JsonPropertyName("quality")] public string? Quality { get; set; }
}