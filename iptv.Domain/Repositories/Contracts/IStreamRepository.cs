using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface IStreamRepository : IMonjoRepository<Streams>
{
    Task<Streams> GetByStreamIdAsync(string streamId, CancellationToken cancellationToken = default);

    Task<Streams> GetByExternalIdAsync(string providerPublicKey, string externalId,
        CancellationToken cancellationToken = default);

    Task<List<Streams>> GetByChannelAsync(string channelId, CancellationToken cancellationToken = default);

    Task<List<Streams>> GetByProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default);

    Task<List<Streams>> GetStaleStreamsAsync(DateTime olderThan, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A provider's documents needed by a sync: every ACTIVE one, plus inactive ones whose ExternalId
    /// is in <paramref name="fetchedExternalIds"/> (so they are reactivated, not inserted twice).
    /// Deactivated out-of-scope documents are not reloaded on every sync.
    /// </summary>
    Task<List<Streams>> GetForSyncAsync(string providerPublicKey, IReadOnlyCollection<string> fetchedExternalIds,
        CancellationToken cancellationToken = default);
}
