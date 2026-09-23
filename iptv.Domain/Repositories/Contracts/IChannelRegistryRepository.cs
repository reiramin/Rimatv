using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface IChannelRegistryRepository : IMonjoRepository<ChannelRegistry>
{
    Task<ChannelRegistry> GetByCanonicalIdAsync(string canonicalId,
        CancellationToken cancellationToken = default);

    Task<List<ChannelRegistry>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}
