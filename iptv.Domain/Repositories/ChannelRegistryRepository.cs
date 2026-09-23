using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class ChannelRegistryRepository(IMonjoConnection connection)
    : MonjoRepository<ChannelRegistry>(connection),
        IChannelRegistryRepository, RegisterMode.ISingletonDependency
{
    public async Task<ChannelRegistry> GetByCanonicalIdAsync(string canonicalId,
        CancellationToken cancellationToken = default)
        => await AsQueryable().FirstOrDefaultAsync(q => q.CanonicalId == canonicalId, cancellationToken);

    public async Task<List<ChannelRegistry>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await AsQueryable().Where(q => !q.Inactive).ToListAsync(cancellationToken);
}
