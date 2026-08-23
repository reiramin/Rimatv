using Utilities.Attributes;

namespace iptv.Services._IptvProvider.DTOs.Updates;

public class IptvProviderEditUpdate
{
    [StringInputValidation] public string PublicKey { get; set; }

    [StringInputValidation]public string Name { get; set; }
    [StringInputValidation(isRequired:false)]public string BaseUrl { get; set; }
    [StringInputValidation(isRequired:false)]public string ChannelsEndpoint { get; set; }
    [StringInputValidation(isRequired:false)]public string StreamsEndpoint { get; set; }
    [StringInputValidation(isRequired:false)]public string LogosEndpoint { get; set; }
    [StringInputValidation(isRequired:false)]public string ApiKey { get; set; }
    public bool Inactive { get; set; }
}