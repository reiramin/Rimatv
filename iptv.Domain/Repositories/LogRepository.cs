using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Domain.Repositories
{
    public class LogRepository(IMonjoConnection connection) : MonjoRepository<Log>(connection), ILogRepository, RegisterMode.ISingletonDependency
    {
    }
}
