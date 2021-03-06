﻿using MongoDB.Bson;
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

        Task InsertIntoDBAsync(DateTime parsedDateEvent, BsonArray workshopData);

        Task ReplaceDataOfDBAsync(DateTime parsedDateEvent, BsonArray workshopData);

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
            var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTIONSTRING", EnvironmentVariableTarget.Process);
            var database = Environment.GetEnvironmentVariable("MONGODB_DATABASE", EnvironmentVariableTarget.Process);

            var dbClient = new MongoClient(connectionString);
          
            var dbServer = dbClient.GetDatabase(database);
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

        // Build the data for the database
        internal static BsonDocument BuildEventDocument(DateTime parsedDateEvent, BsonArray workshopData)
        {
            var eventData = new BsonDocument();
            eventData.AddRange(new Dictionary<string, object> {
                { "date", parsedDateEvent },
                { "type", "CoderDojo Virtual" },
                { "location", "CoderDojo Online" },
                { "workshops", workshopData}
            });

            if (workshopData == null || workshopData.Count == 0)
            {
                eventData["location"] += " - Themen werden noch bekannt gegeben";
            }
            return eventData;
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
                dateFilter = new BsonDocument("date", new BsonDocument { { "$gte", DateTime.Today } });
            }
            else
            {
                dateFilter = new BsonDocument();
            }

            return await ReadFromDBAsync<Event>(collectionEvents, dateFilter);
        }

        public async Task<IEnumerable<Mentor>> ReadMentorsFromDBAsync() =>
            await ReadFromDBAsync<Mentor>(collectionMentors, new BsonDocument());

        public async Task InsertIntoDBAsync(DateTime parsedDateEvent, BsonArray workshopData) =>
            await collectionEvents.InsertOneAsync(BuildEventDocument(parsedDateEvent, workshopData));

        public async Task ReplaceDataOfDBAsync(DateTime parsedDateEvent, BsonArray workshopData)
        {
            var dateFilter = new BsonDocument("date", parsedDateEvent);
            await collectionEvents.ReplaceOneAsync(dateFilter, BuildEventDocument(parsedDateEvent, workshopData));
        }
    }
}