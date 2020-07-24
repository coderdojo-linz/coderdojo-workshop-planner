using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CDWPlanner.DTO;
using MongoDB.Bson.Serialization;

namespace CDWPlanner
{
    public interface IDataAccess
    {
        Task<BsonDocument> ReadWorkshopForDateAsync(DateTime date);
        Task InsertIntoDBAsync(BsonDocument eventData);
        Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData);
        Task<IEnumerable<Event>> ReadWorkshopFromEvents(string past);
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
        public async Task<IEnumerable<Event>> ReadWorkshopFromEvents(string past)
        {
            var date = DateTime.Today;
            var eventsList = new List<Event>();
            var eventsListAsync = new List<Event>();

            if (past == "false")
            {
                var dateFilter = new BsonDocument("date", new BsonDocument {{ "$gte", date }});
                var dbEvents = await collection.FindAsync(dateFilter);
                foreach(var doc in await dbEvents.ToListAsync())
                {
                    eventsList.Add(BsonSerializer.Deserialize<Event>(doc));
                }
                return eventsList;
            }

            var dbEventsFoundWithoutFilter = await collection.FindAsync(new BsonDocument());
            foreach (var doc in await dbEventsFoundWithoutFilter.ToListAsync())
            {
                eventsListAsync.Add(BsonSerializer.Deserialize<Event>(doc));
            }
            return eventsListAsync;
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
