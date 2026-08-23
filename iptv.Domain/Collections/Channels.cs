using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

[MonjoCollectionName("Channels")]
public class Channels : BaseDocument
{
    public string ChannelId { get; set; }= Guid.NewGuid().ToString("N");
    public string ProviderPublicKey { get; set; }
    public string ExternalId { get; set; }
    public string Name { get; set; }
    public string ImageUri { get; set; }
    public string Country { get; set; }
    public string Category { get; set; }
    public string CurrentStreamId { get; set; }
    public bool Inactive { get; set; }
    public string DataHash { get; set; }
}