using iptv.Domain.Collections;
using Utilities.Attributes;

namespace iptv.Services._IptvProvider.DTOs.Updates;

public class IptvProviderCreateUpdate
{
    [StringInputValidation(isRequired: true)]
    public string Name { get; set; }
    [StringInputValidation(isRequired: true)]
    public string BaseUrl { get; set; }
    [StringInputValidation(isRequired: true)]
    public string ChannelsEndpoint { get; set; }
    [StringInputValidation(isRequired: false)]
    public string StreamsEndpoint { get; set; }
    [StringInputValidation(isRequired: false)]
    public string LogosEndpoint { get; set; }
    public string ApiKey { get; set; }
    public bool Inactive { get; set; }
    public ProviderKind Kind { get; set; } = ProviderKind.Generic;
    // public bool UseProxy { get; set; }
}