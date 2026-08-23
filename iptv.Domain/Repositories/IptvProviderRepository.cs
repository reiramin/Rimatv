using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories;

public class IptvProviderRepository(IMonjoConnection connection) : MonjoRepository<IptvProviders>(connection),
    IIptvProviderRepository, RegisterMode.ISingletonDependency
{
    public async Task<IptvProviders> GetByPublicKeyAsync(string publicKey)
        => await AsQueryable().FirstOrDefaultAsync(tv => tv.PublicKey == publicKey) ??
           throw new NotFoundException("IPTV provider not found.");
}