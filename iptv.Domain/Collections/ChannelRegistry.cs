using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

/// <summary>
/// The canonical catalogue of wanted channels (~800 entries), keyed by <see cref="CanonicalId"/>.
/// Replaces the hard-coded name list in CuratedChannelWhitelist. Seeded idempotently from
/// docs/agent/channel_registry.seed.json (embedded resource).
/// </summary>
[MonjoCollectionName("ChannelRegistry")]
public class ChannelRegistry : BaseDocument
{
    public string CanonicalId { get; set; }

    public string Name { get; set; }
    public string NameFa { get; set; }

    public List<string> Aliases { get; set; } = [];

    public string CuratedCountry { get; set; }
    public int CuratedRank { get; set; }

    public List<string> Categories { get; set; } = [];

    // iptv-org source country of the channel (e.g. TR for GEM TV) — informational.
    public string SourceCountry { get; set; }

    public bool RequiresIranianIp { get; set; }
    public bool GeoBlockedEverywhere { get; set; }

    public bool Inactive { get; set; }

    // Idempotent-seed bookkeeping: SeedHash detects seed-content changes;
    // AdminEdited protects admin edits of Inactive/CuratedRank from being overwritten.
    public string SeedHash { get; set; }
    public bool AdminEdited { get; set; }
}
