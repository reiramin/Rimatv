using System.Text.Json.Serialization;

namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalChannel
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("country")] public string Country { get; set; }
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = [];
    [JsonPropertyName("logo")] public string Logo { get; set; }

    // iptv-org channels.json additions (additive; tolerate absence).
    [JsonPropertyName("alt_names")] public List<string> AltNames { get; set; } = [];
    [JsonPropertyName("network")] public string Network { get; set; }
    [JsonPropertyName("is_nsfw")] public bool IsNsfw { get; set; }
    [JsonPropertyName("closed")] public string Closed { get; set; }
    [JsonPropertyName("replaced_by")] public string ReplacedBy { get; set; }

    // Fetcher-populated (not part of the wire schema).
    [JsonIgnore] public string Feed { get; set; }
    [JsonIgnore] public List<string> Languages { get; set; } = [];
    [JsonIgnore] public List<string> Labels { get; set; } = [];
    [JsonIgnore] public string TvgId { get; set; }

    // Canonical id hint from the source (e.g. iptv-org id or tvg-id). When absent the
    // sync resolves it via the registry / fallback scheme.
    [JsonIgnore] public string CanonicalIdHint { get; set; }

    // Source file of providers with one file per country, e.g. "famelack:ir" (persisted on Channels).
    [JsonIgnore] public string SourceTag { get; set; }
}
