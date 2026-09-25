using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace iptv.Domain.Collections;

/// <summary>
/// Tiny marker collection for one-off data migrations (e.g. "dedupe-v1"), so expensive startup
/// work runs once instead of on every boot of the free host.
/// </summary>
[MonjoCollectionName("Migrations")]
public class Migrations : BaseDocument
{
    public string Name { get; set; }
    public DateTime AppliedMoment { get; set; } = DateTime.UtcNow;
    public string Note { get; set; }
}
