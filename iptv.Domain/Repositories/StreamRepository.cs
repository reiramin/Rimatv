using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class StreamRepository(IMonjoConnection connection) : MonjoRepository<Streams>(connection),
    IStreamRepository, RegisterMode.ISingletonDependency
{
    public async Task<Streams> GetByStreamIdAsync(string streamId, CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(q => q.StreamId == streamId, cancellationToken);

    public async Task<Streams> GetByExternalIdAsync(string providerPublicKey, string externalId,
        CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(
            q => q.ProviderPublicKey == providerPublicKey && q.ExternalId == externalId,
            cancellationToken);

    public async Task<List<Streams>> GetByChannelAsync(string channelId, CancellationToken cancellationToken = default)
        => await AsQueryable().Where(q => q.ChannelId == channelId).ToListAsync(cancellationToken);

    public async Task<List<Streams>> GetByProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default)
        => await AsQueryable().Where(q => q.ProviderPublicKey == providerPublicKey).ToListAsync(cancellationToken);

    public async Task<List<Streams>> GetStaleStreamsAsync(DateTime olderThan, int limit,
        CancellationToken cancellationToken = default)
        => await AsQueryable()
            .Where(q => !q.Inactive && (q.LastCheckedMoment == null || q.LastCheckedMoment < olderThan))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<List<Streams>> GetForSyncAsync(string providerPublicKey,
        IReadOnlyCollection<string> fetchedExternalIds, CancellationToken cancellationToken = default)
    {
        var ids = fetchedExternalIds?.ToList() ?? [];
        return await AsQueryable()
            .Where(q => q.ProviderPublicKey == providerPublicKey && (!q.Inactive || ids.Contains(q.ExternalId)))
            .ToListAsync(cancellationToken);
    }
}
