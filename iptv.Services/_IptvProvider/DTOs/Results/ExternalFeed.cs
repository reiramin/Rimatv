using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Results;

// iptv-org feeds.json entry: languages per (channel, feed).
public class ExternalFeed
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("is_main")] public bool IsMain { get; set; }
    [JsonPropertyName("broadcast_area")] public List<string> BroadcastArea { get; set; } = [];
    [JsonPropertyName("languages")] public List<string> Languages { get; set; } = [];
    [JsonPropertyName("format")] public string Format { get; set; }
}

// iptv-org blocklist.json entry.
public class ExternalBlocklistEntry
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; }
    [JsonPropertyName("ref")] public string Ref { get; set; }
}
