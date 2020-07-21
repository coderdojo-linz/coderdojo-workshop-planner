using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public interface IDataAccess
    {
        Task<BsonDocument> ReadWorkshopForDateAsync(DateTime date);
        Task InsertIntoDBAsync(BsonDocument eventData);
        Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData);
    };

    public class DataAccess : IDataAccess
    {
        private readonly IMongoCollection<BsonDocument> collection;

        public DataAccess()
        {
            collection = GetCollectionFromServer();
        }

        //Connect with server and get collection
        private IMongoCollection<BsonDocument> GetCollectionFromServer()
        {
            var dbUser = Environment.GetEnvironmentVariable("MONGOUSER", EnvironmentVariableTarget.Process);
            var dbPassword = Environment.GetEnvironmentVariable("MONGOPASSWORD", EnvironmentVariableTarget.Process);
            var dbString = Environment.GetEnvironmentVariable("MONGODB", EnvironmentVariableTarget.Process);
            var dbConnection = Environment.GetEnvironmentVariable("MONGOCONNECTION", EnvironmentVariableTarget.Process);
            var dbCollectionString = Environment.GetEnvironmentVariable("MONGOCOLLECTION", EnvironmentVariableTarget.Process);

            var urlMongo = $"mongodb://{dbUser}:{dbPassword}@{dbConnection}/{dbString}/?retryWrites=false";
            var dbClient = new MongoClient(urlMongo);

            var dbServer = dbClient.GetDatabase($"{dbString}");
            var dbCollection = dbServer.GetCollection<BsonDocument>($"{dbCollectionString}");

            return dbCollection;
        }

        public async Task<BsonDocument> ReadWorkshopForDateAsync(DateTime date)
        {
            var dateFilter = new BsonDocument("date", date);
            var dbEvents = await collection.FindAsync(dateFilter);
            var dbEventsFound = await dbEvents.ToListAsync();
            return dbEventsFound.FirstOrDefault();
        }

        public async Task InsertIntoDBAsync(BsonDocument eventData)
        {
            var collection = GetCollectionFromServer();
            await collection.InsertOneAsync(eventData);
        }

        public async Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData)
        {
            var dateFilter = new BsonDocument("date", date);
            var collection = GetCollectionFromServer();
            await collection.ReplaceOneAsync(dateFilter, eventData);
        }
    }
}
