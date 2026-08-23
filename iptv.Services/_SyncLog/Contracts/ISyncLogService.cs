using iptv.Services._SyncLog.DTOs.Results;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._SyncLog.Contracts;

public interface ISyncLogService
{
    Task<MonjoFilteredResult<SyncLogFilteredResult>> GetAllAsync(MonjoQuery query);

    Task<SyncLogFilteredResult> GetByPublicKeyAsync(string publicKey);
}
