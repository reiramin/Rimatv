using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using iptv.Services._SyncLog.Contracts;
using iptv.Services._SyncLog.DTOs.Results;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;

namespace iptv.Services._SyncLog;

public class SyncLogService(ISyncLogRepository _syncLogRepository)
    : ISyncLogService, RegisterMode.IScopedDependency
{
    public async Task<MonjoFilteredResult<SyncLogFilteredResult>> GetAllAsync(MonjoQuery query)
    {
        query.WithBase<SyncLogFilteredResult>();

        return await _syncLogRepository
            .AsQueryable()
            .Apply(query.Where, nameof(SyncLogFilteredResult))
            .Apply(query.Order, nameof(SyncLogFilteredResult))
            .Select(syncLog => new SyncLogFilteredResult
            {
                PublicKey = syncLog.PublicKey,
                ProviderPublicKey = syncLog.ProviderPublicKey,
                ProviderName = syncLog.ProviderName,
                Status = syncLog.Status.ToString(),
                TotalChannels = syncLog.TotalChannels,
                InsertedChannels = syncLog.InsertedChannels,
                UpdatedChannels = syncLog.UpdatedChannels,
                DeactivatedChannels = syncLog.DeactivatedChannels,
                TotalStreams = syncLog.TotalStreams,
                InsertedStreams = syncLog.InsertedStreams,
                UpdatedStreams = syncLog.UpdatedStreams,
                DeactivatedStreams = syncLog.DeactivatedStreams,
                Message = syncLog.Message,
                StartMoment = syncLog.StartMoment,
                EndMoment = syncLog.EndMoment
            })
            .ExecuteAsync(query, nameof(SyncLogFilteredResult));
    }

    public async Task<SyncLogFilteredResult> GetByPublicKeyAsync(string publicKey)
    {
        var syncLog = await _syncLogRepository.GetByPublicKeyAsync(publicKey)
                      ?? throw new NotFoundException("Sync log not found.");

        return new SyncLogFilteredResult
        {
            PublicKey = syncLog.PublicKey,
            ProviderPublicKey = syncLog.ProviderPublicKey,
            ProviderName = syncLog.ProviderName,
            Status = syncLog.Status.ToString(),
            TotalChannels = syncLog.TotalChannels,
            InsertedChannels = syncLog.InsertedChannels,
            UpdatedChannels = syncLog.UpdatedChannels,
            DeactivatedChannels = syncLog.DeactivatedChannels,
            TotalStreams = syncLog.TotalStreams,
            InsertedStreams = syncLog.InsertedStreams,
            UpdatedStreams = syncLog.UpdatedStreams,
            DeactivatedStreams = syncLog.DeactivatedStreams,
            Message = syncLog.Message,
            StartMoment = syncLog.StartMoment,
            EndMoment = syncLog.EndMoment
        };
    }
}
