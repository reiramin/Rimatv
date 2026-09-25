using System.Text.Json.Serialization;

namespace iptv.Services._StreamProbe.DTOs;

/// <summary>One deep-probe result from RimaTv_Data/scripts/validate.py.</summary>
public class ProbeResultItem
{
    [JsonPropertyName("streamId")] public string StreamId { get; set; }

    // ok | dead | region | unverified
    [JsonPropertyName("status")] public string Status { get; set; }

    [JsonPropertyName("webCompatible")] public bool? WebCompatible { get; set; }
    [JsonPropertyName("regionHint")] public string RegionHint { get; set; }
    [JsonPropertyName("moment")] public DateTime? Moment { get; set; }
}

public class ProbeReportResult
{
    public int Received { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }
}
