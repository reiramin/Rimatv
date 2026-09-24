using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

[MonjoCollectionName("Channels")]
public class Channels : BaseDocument
{
    public string ChannelId { get; set; }= Guid.NewGuid().ToString("N");
    public string ProviderPublicKey { get; set; }
    public string ExternalId { get; set; }

    // Cross-provider identity: resolved during sync (iptv-org id / tvg-id, registry alias, or ext:{provider}:{id}).
    public string CanonicalId { get; set; }

    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }

    // iptv-org feed id (when a channel is split per feed because languages differ).
    public string Feed { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Labels { get; set; } = [];

    // Source file for providers with one file per country, e.g. "famelack:ir". Drives the ingest scope.
    public string SourceTag { get; set; }

    public string CurrentStreamId { get; set; }

    public bool Inactive { get; set; }

    // Set by the admin Activate endpoint; sync never clears it.
    public bool AdminDisabled { get; set; }

    public string DataHash { get; set; }
}
