using CDWPlanner;
using CDWPlanner.DTO;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CDWPlanner.Tests
{
    public class EventDataAccessTests
    {
        [Fact]
        public async Task AddNewEvent()
        {
            var dataAccessMock = new Mock<IDataAccess>();
            dataAccessMock.Setup(da => da.ReadWorkshopForDateAsync(It.IsAny<DateTime>()))
                .Returns(Task.FromResult<BsonDocument>(null));
            BsonDocument insertedDocument = null;
            dataAccessMock.Setup(da => da.InsertIntoDBAsync(It.IsAny<BsonDocument>()))
                .Callback<BsonDocument>(doc => insertedDocument = doc);

            var func = new PlanEvent(null, dataAccessMock.Object);
            await func.Receive(@"
            {
              ""Operation"": ""added"",
              ""FolderInfo"": {
                ""FullFolder"": ""2020-07-17/PLAN.yml"",
                ""DateFolder"": ""2020-07-17"",
                ""File"": ""PLAN.yml""
              },
              ""Workshops"": {
                ""workshops"": [
                  {
                    ""begintime"": ""2020-07-17T13:45:00"",
                    ""endtime"": ""2020-07-17T15:45:00"",
                    ""draft"": false,
                    ""title"": ""Test"",
                    ""targetAudience"": ""TestAudience"",
                    ""description"": ""TestDescription *with* markup"",
                    ""prerequisites"": ""TestPrerequisites"",
                    ""mentors"": [ ""Foo"", ""Bar"" ],
                    ""zoom"": ""https://us02web.zoom.us/...""
                  }
                ]
              }
            }", Mock.Of<ILogger>());

            dataAccessMock.Verify(da => da.ReadWorkshopForDateAsync(It.IsAny<DateTime>()), Times.Once);
            dataAccessMock.Verify(da => da.InsertIntoDBAsync(It.IsAny<BsonDocument>()), Times.Once);

            Assert.Single(insertedDocument["workshops"] as BsonArray);
        }

        [Fact]
        public void ConvertWorkshopToBson()
        {
            var workshop = new Workshop {
                begintime = new DateTime(2020, 7, 17, 13, 45, 0, 0, DateTimeKind.Utc),
                endtime = new DateTime(2020, 7, 17, 15, 45, 0, 0, DateTimeKind.Utc),
                description = "TestDescription *with* markup",
                mentors = new List<string>() { "Foo", "Bar" },
                prerequisites = "TestPrerequisites",
                targetAudience = "TestAudience",
                title = "Test",
                zoom = "https://us02web.zoom.us/..."
            };

            var bsonDocument = (BsonDocument)workshop;

            Assert.Equal(new BsonDateTime(workshop.begintime), bsonDocument["begintime"]);
            Assert.Equal(new BsonDateTime(workshop.endtime), bsonDocument["endtime"]);
            Assert.Equal(workshop.description, bsonDocument["description"]);
            Assert.Equal(new BsonArray(workshop.mentors), bsonDocument["mentors"]);
            Assert.Equal(workshop.prerequisites, bsonDocument["prerequisites"]);
            Assert.Equal(workshop.targetAudience, bsonDocument["targetAudience"]);
            Assert.Equal(workshop.title, bsonDocument["title"]);
            Assert.Equal(workshop.zoom, bsonDocument["zoom"]);
        }
    }
}
