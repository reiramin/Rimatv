using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

[MonjoCollectionName("IptvProviders")]
public class IptvProviders : BaseDocument
{
    public string PublicKey { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    
    public string BaseUrl { get; set; }
    public string ChannelsEndpoint { get; set; }
    public string StreamsEndpoint { get; set; }
    public string LogosEndpoint { get; set; }

    public string ApiKey { get; set; }

    public bool Inactive { get; set; }

    public DateTime? LastSyncMoment { get; set; }
    
    public string LastSyncStatus { get; set; }
    
    public ProviderKind Kind { get; set; } = ProviderKind.Generic;
    
    public int FetchTimeoutSeconds { get; set; } = 0;

    // public bool UseProxy { get; set; }
}

public enum ProviderKind { Generic, Pluto }