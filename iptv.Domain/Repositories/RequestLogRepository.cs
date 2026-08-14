using iptv.Domain.Collections;
using iptv.Domain.Repositories.Contracts;
using Utilities.Constants;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;


namespace iptv.Domain.Repositories
{
    public class RequestLogRepository(IMonjoConnection connection)
        : MonjoRepository<RequestLog>(connection), IRequestLogRepository, RegisterMode.ISingletonDependency
    {
    }
}
