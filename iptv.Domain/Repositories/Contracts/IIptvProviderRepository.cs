using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts;

public interface IIptvProviderRepository : IMonjoRepository<IptvProviders>
{
    Task<IptvProviders> GetByPublicKeyAsync(string publicKey);

}