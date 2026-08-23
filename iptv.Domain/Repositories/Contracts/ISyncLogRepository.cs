using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface ISyncLogRepository : IMonjoRepository<SyncLogs>
{
    Task<SyncLogs> GetByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default);
}
