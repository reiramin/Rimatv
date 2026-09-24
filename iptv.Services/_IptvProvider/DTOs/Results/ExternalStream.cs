using System.Text.Json.Serialization;


namespace iptv.Services._IptvProvider.DTOs.Results;

public class ExternalStream
{
    [JsonPropertyName("channel")] public string Channel { get; set; }
    [JsonPropertyName("feed")] public string Feed { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("user_agent")] public string UserAgent { get; set; }
    [JsonPropertyName("referrer")] public string Referrer { get; set; }
    [JsonPropertyName("quality")] public string? Quality { get; set; }

    // 2026-09-18: streams `label` (string) was replaced by `labels` (array).
    // Tolerate both — the fetcher merges them into <see cref="Labels"/>.
    [JsonPropertyName("labels")] public List<string> LabelsArray { get; set; } = [];
    [JsonPropertyName("label")] public string LabelLegacy { get; set; }

    // Fetcher-populated (not part of the wire schema).
    [JsonIgnore] public List<string> Labels { get; set; } = [];
    [JsonIgnore] public List<string> Languages { get; set; } = [];

    // Requires an Iranian IP to play (source label [IR] etc.).
    [JsonIgnore] public bool RequiresIranianIp { get; set; }

    // Geo-blocked (iptv-org "Geo-blocked" label or source marker).
    [JsonIgnore] public bool GeoBlocked { get; set; }

    // When set by a fetcher, sync uses this as the stream's stable ExternalId instead of hashing the
    // URL. Used by Pluto so a channel's stream is UPDATED (not deactivated+reinserted) every sync.
    [JsonIgnore] public string StableExternalId { get; set; }

    // Adaptive master playlist (a.k.a. "Auto") — preferred class in selection.
    [JsonIgnore] public bool IsAdaptive { get; set; }

    // Explicit stream type / region from the fetcher (resolver provider); null = derive in sync.
    [JsonIgnore] public string Type { get; set; }
    [JsonIgnore] public string RequiredRegion { get; set; }

    // Official-source ladder data (resolver provider only).
    [JsonIgnore] public string PageUrl { get; set; }
    [JsonIgnore] public string ResolveMethod { get; set; }
    [JsonIgnore] public string ResolvePattern { get; set; }
    [JsonIgnore] public string ResolveApiUrl { get; set; }
    [JsonIgnore] public Dictionary<string, string> ResolveHeaders { get; set; }
    [JsonIgnore] public int ResolveTtlSeconds { get; set; }
    [JsonIgnore] public bool IpBound { get; set; }
    [JsonIgnore] public string PlayerUrl { get; set; }
    [JsonIgnore] public bool? Embeddable { get; set; }

    /// <summary>Merges the legacy `label` string and the new `labels` array into <see cref="Labels"/>.</summary>
    public List<string> ResolveLabels()
    {
        var merged = new List<string>();
        if (LabelsArray is { Count: > 0 })
            merged.AddRange(LabelsArray.Where(l => !string.IsNullOrWhiteSpace(l)));
        if (!string.IsNullOrWhiteSpace(LabelLegacy))
            merged.Add(LabelLegacy);
        if (Labels is { Count: > 0 })
            merged.AddRange(Labels.Where(l => !string.IsNullOrWhiteSpace(l)));
        return merged
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
