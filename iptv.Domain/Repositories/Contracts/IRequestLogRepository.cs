using iptv.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories.Contracts
{
    public interface IRequestLogRepository : IMonjoRepository<RequestLog>
    {
    }
}
 