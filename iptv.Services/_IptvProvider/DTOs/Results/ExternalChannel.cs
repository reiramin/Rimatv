using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalChannel
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; }
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = [];
    [JsonPropertyName("logo")] public string Logo { get; set; }
}