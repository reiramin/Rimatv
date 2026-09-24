using System.Text.Json.Serialization;

namespace iptv.Services._ChannelRegistry.Seed;

public class RegistrySeedRoot
{
    [JsonPropertyName("_meta")] public RegistrySeedMeta Meta { get; set; }
    [JsonPropertyName("countries")] public Dictionary<string, List<RegistrySeedEntry>> Countries { get; set; } = new();
}

public class RegistrySeedMeta
{
    [JsonPropertyName("legacyWhitelistUnmatched")]
    public List<string> LegacyWhitelistUnmatched { get; set; } = [];
}

public class RegistrySeedEntry
{
    [JsonPropertyName("canonicalId")] public string CanonicalId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("nameFa")] public string NameFa { get; set; }
    [JsonPropertyName("curatedCountry")] public string CuratedCountry { get; set; }
    [JsonPropertyName("curatedRank")] public int CuratedRank { get; set; }
    [JsonPropertyName("sourceCountry")] public string SourceCountry { get; set; }
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = [];
    [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = [];
    [JsonPropertyName("requiresIranianIp")] public bool RequiresIranianIp { get; set; }
    [JsonPropertyName("geoBlockedEverywhere")] public bool GeoBlockedEverywhere { get; set; }
}
