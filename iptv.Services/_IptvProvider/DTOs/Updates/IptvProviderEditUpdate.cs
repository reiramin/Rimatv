using iptv.Domain.Collections;
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
    [StringInputValidation(isRequired:false)]public string FeedsEndpoint { get; set; }
    [StringInputValidation(isRequired:false)]public string BlocklistEndpoint { get; set; }
    [StringInputValidation(isRequired:false)]public string FallbackBaseUrl { get; set; }
    [StringInputValidation(isRequired:false)]public string ApiKey { get; set; }

    public List<string> AdditionalEndpoints { get; set; }
    public Dictionary<string, string> Headers { get; set; }

    public bool Inactive { get; set; }

    // Editable per §2.2.
    public ProviderKind Kind { get; set; } = ProviderKind.Generic;
    public int FetchTimeoutSeconds { get; set; }
    public int Priority { get; set; }
}
