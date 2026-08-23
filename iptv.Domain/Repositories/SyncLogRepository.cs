using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class SyncLogRepository(IMonjoConnection connection) : MonjoRepository<SyncLogs>(connection),
    ISyncLogRepository, RegisterMode.ISingletonDependency
{
    public async Task<SyncLogs> GetByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(q => q.PublicKey == publicKey, cancellationToken);
}
