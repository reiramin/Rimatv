using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface IMigrationRepository : IMonjoRepository<Migrations>
{
    Task<bool> IsAppliedAsync(string name, CancellationToken cancellationToken = default);

    Task MarkAppliedAsync(string name, string note = null, CancellationToken cancellationToken = default);
}
