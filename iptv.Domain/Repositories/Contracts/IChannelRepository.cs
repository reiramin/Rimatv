using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface IChannelRepository : IMonjoRepository<Channels>
{
    Task<Channels> GetByChannelIdAsync(string channelId, CancellationToken cancellationToken = default);

    Task<Channels> GetByExternalIdAsync(string providerPublicKey, string externalId,
        CancellationToken cancellationToken = default);

    Task<List<Channels>> GetByProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default);
}
