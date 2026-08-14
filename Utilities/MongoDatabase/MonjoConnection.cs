using MongoDB.Driver;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;


namespace Utilities.MongoDatabase
{
    public class MonjoConnection : IMonjoConnection, ISingletonDependency
    {
        public IMongoClient Client { get; }
        public IMongoDatabase Database { get; }
        
        public MonjoConnection(IMonjoSettings settings)
        {
            Client = new MongoClient(settings.ConnectionString);
            Database = Client.GetDatabase(settings.DatabaseName);
        }
    }
}
