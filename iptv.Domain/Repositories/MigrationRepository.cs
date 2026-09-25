using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class MigrationRepository(IMonjoConnection connection) : MonjoRepository<Migrations>(connection),
    IMigrationRepository, RegisterMode.ISingletonDependency
{
    public async Task<bool> IsAppliedAsync(string name, CancellationToken cancellationToken = default)
        => await AsQueryable().AnyAsync(q => q.Name == name, cancellationToken);

    public async Task MarkAppliedAsync(string name, string note = null, CancellationToken cancellationToken = default)
        => await InsertOneAsync(new Migrations { Name = name, Note = note }, cancellationToken);
}
