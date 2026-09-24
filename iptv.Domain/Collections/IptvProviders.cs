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

    // Generic (iptv-org) extras.
    public string FeedsEndpoint { get; set; }
    public string BlocklistEndpoint { get; set; }

    // Some providers publish one file per country/region.
    public List<string> AdditionalEndpoints { get; set; } = [];

    public string ApiKey { get; set; }

    // Arbitrary request headers to send to the provider (in addition to ApiKey -> X-Api-Key).
    public Dictionary<string, string> Headers { get; set; } = [];

    // Optional mirror used when BaseUrl is unreachable (e.g. iptv-org raw.githubusercontent gh-pages).
    public string FallbackBaseUrl { get; set; }

    public bool Inactive { get; set; }

    public DateTime? LastSyncMoment { get; set; }

    public string LastSyncStatus { get; set; }

    public ProviderKind Kind { get; set; } = ProviderKind.Generic;

    public int FetchTimeoutSeconds { get; set; } = 0;

    // Higher = preferred when the same canonical channel comes from several providers.
    public int Priority { get; set; } = 0;

    // public bool UseProxy { get; set; }
}

// NOTE: numeric values are persisted in Mongo; only APPEND new kinds, never reorder.
public enum ProviderKind
{
    Generic = 0,
    Pluto = 1,
    M3u = 2,
    FamelackJson = 3,
    ZappJson = 4,

    // Official-source ladder from the embedded iptv.Services/_Resolver/resolvers.json.
    Resolver = 5
}
