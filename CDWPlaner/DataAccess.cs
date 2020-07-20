using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CDWPlaner
{
    public class DataAccess
    {
        private IMongoCollection<BsonDocument> collection;

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

        public async Task InsertIntoDB(BsonDocument eventData)
        {
            var collection = GetCollectionFromServer();
            collection.InsertOne(eventData);
        }
        public async Task ReplaceDataOfDB(DateTime date, BsonDocument eventData)
        {
            var dateFilter = new BsonDocument("date", date);
            var collection = GetCollectionFromServer();
            collection.ReplaceOne(dateFilter, eventData);
        }
    }
}
