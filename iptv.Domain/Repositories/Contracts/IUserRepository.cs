using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts
{
    public interface IUserRepository : IMonjoRepository<User>
    {
        Task<User> GetUserByPublicKeyAsync(string publicKey);
    }
}