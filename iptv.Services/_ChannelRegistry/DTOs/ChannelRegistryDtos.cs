using Utilities.Attributes;

namespace iptv.Services._ChannelRegistry.DTOs;

public class ChannelRegistryResult
{
    public string CanonicalId { get; set; }
    public string Name { get; set; }
    public string NameFa { get; set; }
    public List<string> Aliases { get; set; }
    public string CuratedCountry { get; set; }
    public int CuratedRank { get; set; }
    public List<string> Categories { get; set; }
    public string SourceCountry { get; set; }
    public bool RequiresIranianIp { get; set; }
    public bool GeoBlockedEverywhere { get; set; }
    public bool Inactive { get; set; }
    public bool AdminEdited { get; set; }
}

public class ChannelRegistryCreateUpdate
{
    [StringInputValidation] public string CanonicalId { get; set; }
    [StringInputValidation] public string Name { get; set; }
    [StringInputValidation(isRequired: false)] public string NameFa { get; set; }
    [StringInputValidation] public string CuratedCountry { get; set; }
    public int CuratedRank { get; set; }
    public List<string> Aliases { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    [StringInputValidation(isRequired: false)] public string SourceCountry { get; set; }
    public bool RequiresIranianIp { get; set; }
    public bool GeoBlockedEverywhere { get; set; }
}

public class ChannelRegistryEditUpdate
{
    [StringInputValidation] public string CanonicalId { get; set; }
    [StringInputValidation(isRequired: false)] public string Name { get; set; }
    [StringInputValidation(isRequired: false)] public string NameFa { get; set; }
    [StringInputValidation(isRequired: false)] public string CuratedCountry { get; set; }
    public int CuratedRank { get; set; }
    public List<string> Aliases { get; set; }
    public List<string> Categories { get; set; }
    [StringInputValidation(isRequired: false)] public string SourceCountry { get; set; }
    public bool RequiresIranianIp { get; set; }
    public bool GeoBlockedEverywhere { get; set; }
}

public class ChannelRegistryActivateUpdate
{
    [StringInputValidation] public string CanonicalId { get; set; }
    public bool ShouldActivate { get; set; }
}
