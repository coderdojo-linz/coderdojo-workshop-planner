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
        Task<Event> ReadEventForDateFromDBAsync(DateTime date);
        Task InsertIntoDBAsync(BsonDocument eventData);
        Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData);
        Task<IEnumerable<Event>> ReadEventsFromDBAsync(bool past);
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

        /// <summary>
        /// Connect with server and get collection
        /// </summary>
        private IMongoCollection<BsonDocument> GetCollectionFromServer(string dbCollectionString)
        {
            var dbUser = Environment.GetEnvironmentVariable("MONGOUSER", EnvironmentVariableTarget.Process);
            var dbPassword = Environment.GetEnvironmentVariable("MONGOPASSWORD", EnvironmentVariableTarget.Process);
            var dbString = Environment.GetEnvironmentVariable("MONGODB", EnvironmentVariableTarget.Process);
            var dbConnection = Environment.GetEnvironmentVariable("MONGOCONNECTION", EnvironmentVariableTarget.Process);

            var urlMongo = $"mongodb://{dbUser}:{dbPassword}@{dbConnection}/{dbString}/?retryWrites=false";
            var dbClient = new MongoClient(urlMongo);

            var dbServer = dbClient.GetDatabase($"{dbString}");
            var dbCollection = dbServer.GetCollection<BsonDocument>($"{dbCollectionString}");

            return dbCollection;
        }

        private async Task<IEnumerable<T>> ReadFromDBAsync<T>(IMongoCollection<BsonDocument> source, BsonDocument filter)
        {
            var result = new List<T>();
            var queryResult = await source.FindAsync(filter);
            foreach (var m in await queryResult.ToListAsync())
            {
                result.Add(BsonSerializer.Deserialize<T>(m));
            }

            return result;
        }
        

        public async Task<Event> ReadEventForDateFromDBAsync(DateTime date)
        {
            var dateFilter = new BsonDocument("date", date);
            var dbEventsFound = await ReadFromDBAsync<Event>(collectionEvents, dateFilter);
            return dbEventsFound.FirstOrDefault();
        }

        public async Task<IEnumerable<Event>> ReadEventsFromDBAsync(bool past)
        {
            BsonDocument dateFilter;
            if (!past)
            {
                dateFilter = new BsonDocument("date", new BsonDocument {{ "$gte", DateTime.Today }});
            }
            else
            {
                dateFilter = new BsonDocument();
            }

            return await ReadFromDBAsync<Event>(collectionEvents, dateFilter);
        }

        public async Task<IEnumerable<Mentor>> ReadMentorsFromDBAsync() =>
            await ReadFromDBAsync<Mentor>(collectionMentors, new BsonDocument());

        public async Task InsertIntoDBAsync(BsonDocument eventData) =>
            await collectionEvents.InsertOneAsync(eventData);

        public async Task ReplaceDataOfDBAsync(DateTime date, BsonDocument eventData)
        {
            var dateFilter = new BsonDocument("date", date);
            await collectionEvents.ReplaceOneAsync(dateFilter, eventData);
        }
    }
}
