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
        Task<IEnumerable<Event>> ReadWorkshopFromEventsAsync(string past);
        Task<IEnumerable<Mentor>> ReadMentorsFromDBAsync();
    };

    public class DataAccess : IDataAccess
    {
        private readonly IMongoCollection<BsonDocument> collectionEvents;
        private readonly IMongoCollection<BsonDocument> collectionMentors;

        public DataAccess()
        {
            collectionEvents = GetCollectionFromServer(Environment.GetEnvironmentVariable("MONGOCOLLECTIONEVENTS", EnvironmentVariableTarget.Process));
            collectionMentors = GetCollectionFromServer(Environment.GetEnvironmentVariable("MONGOCOLLECTIONMENTORS", EnvironmentVariableTarget.Process));
        }

        //Connect with server and get collection
        private IMongoCollection<BsonDocument> GetCollectionFromServer(string dbCollectionString)
        {
            var dbUser = Environment.GetEnvironmentVariable("MONGOUSER", EnvironmentVariableTarget.Process);
            var dbPassword = Environment.GetEnvironmentVariable("MONGOPASSWORD", EnvironmentVariableTarget.Process);
            var dbString = Environment.GetEnvironmentVariable("MONGODB", EnvironmentVariableTarget.Process);
            var dbConnection = Environment.GetEnvironmentVariable("MONGOCONNECTION", EnvironmentVariableTarget.Process);
           // var dbCollectionString = Environment.GetEnvironmentVariable("MONGOCOLLECTION", EnvironmentVariableTarget.Process);

            var urlMongo = $"mongodb://{dbUser}:{dbPassword}@{dbConnection}/{dbString}/?retryWrites=false";
            var dbClient = new MongoClient(urlMongo);

            var dbServer = dbClient.GetDatabase($"{dbString}");
            var dbCollection = dbServer.GetCollection<BsonDocument>($"{dbCollectionString}");

            return dbCollection;
        }

        public async Task<BsonDocument> ReadWorkshopForDateAsync(DateTime date)
        {
            var dateFilter = new BsonDocument("date", date);
            var dbEvents = await collectionEvents.FindAsync(dateFilter);
            var dbEventsFound = await dbEvents.ToListAsync();
            return dbEventsFound.FirstOrDefault();
        }
        public async Task<IEnumerable<Event>> ReadWorkshopFromEventsAsync(string past)
        {
            var date = DateTime.Today;
            var eventsList = new List<Event>();
            var eventsListAsync = new List<Event>();

            if (past == "false")
            {
                var dateFilter = new BsonDocument("date", new BsonDocument {{ "$gte", date }});
                var dbEvents = await collectionEvents.FindAsync(dateFilter);
                foreach(var doc in await dbEvents.ToListAsync())
                {
                    eventsList.Add(BsonSerializer.Deserialize<Event>(doc));
                }
                return eventsList;
            }

            var dbEventsFoundWithoutFilter = await collectionEvents.FindAsync(new BsonDocument());
            foreach (var doc in await dbEventsFoundWithoutFilter.ToListAsync())
            {
                eventsListAsync.Add(BsonSerializer.Deserialize<Event>(doc));
            }
            return eventsListAsync;
        }

        public async Task<IEnumerable<Mentor>> ReadMentorsFromDBAsync()
        {
            var mentorsListAsync = new List<Mentor>();
            var dbMentors = await collectionMentors.FindAsync(new BsonDocument());
            foreach (var m in await dbMentors.ToListAsync())
            {
                mentorsListAsync.Add(BsonSerializer.Deserialize<Mentor>(m));
            }
            return mentorsListAsync;
        }

        public async Task InsertIntoDBAsync(BsonDocument eventData)
        {
            var collection = GetCollectionFromServer(Environment.GetEnvironmentVariable("MONGOCOLLECTIONEVENTS", EnvironmentVariableTarget.Process));
            await collection.InsertOneAsync(eventData);
        }

        public async Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData)
        {
            var dateFilter = new BsonDocument("date", date);
            var collection = GetCollectionFromServer(Environment.GetEnvironmentVariable("MONGOCOLLECTIONEVENTS", EnvironmentVariableTarget.Process));
            await collection.ReplaceOneAsync(dateFilter, eventData);
        }
    }
}
