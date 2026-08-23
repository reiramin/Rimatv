using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class ChannelRepository(IMonjoConnection connection) : MonjoRepository<Channels>(connection),
    IChannelRepository, RegisterMode.ISingletonDependency
{
    public async Task<Channels> GetByChannelIdAsync(string channelId, CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(q => q.ChannelId == channelId, cancellationToken);

    public async Task<Channels> GetByExternalIdAsync(string providerPublicKey, string externalId,
        CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(
            q => q.ProviderPublicKey == providerPublicKey && q.ExternalId == externalId,
            cancellationToken);

    public async Task<List<Channels>> GetByProviderAsync(string providerPublicKey,
        CancellationToken cancellationToken = default)
        => await AsQueryable().Where(q => q.ProviderPublicKey == providerPublicKey).ToListAsync(cancellationToken);
}
