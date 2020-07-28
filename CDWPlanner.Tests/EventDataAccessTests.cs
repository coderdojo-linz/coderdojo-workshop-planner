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
            dataAccessMock.Setup(da => da.ReadEventForDateFromDBAsync(It.IsAny<DateTime>()))
                .Returns(Task.FromResult<Event>(null));
            BsonDocument insertedDocument = null;
            dataAccessMock.Setup(da => da.InsertIntoDBAsync(It.IsAny<BsonDocument>()))
                .Callback<BsonDocument>(doc => insertedDocument = doc);
            var planZoomMeetingMock = new Mock<IPlanZoomMeeting>();
            planZoomMeetingMock.Setup(z => z.CreateZoomMeetingAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new Meeting { join_url = "Dummy" }));

            var func = new PlanEvent(null, dataAccessMock.Object, planZoomMeetingMock.Object);
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
                    ""begintime"": ""13:45"",
                    ""endtime"": ""15:45"",
                    ""draft"": false,
                    ""title"": ""Test"",
                    ""targetAudience"": ""TestAudience"",
                    ""description"": ""TestDescription *with* markup"",
                    ""prerequisites"": ""TestPrerequisites"",
                    ""mentors"": [ ""Foo"", ""Bar"" ],
                    ""shortCode"": ""Test"",
                    ""zoomUser"": ""Test"",
                    ""zoom"": ""Test""
                  }
                ]
              }
            }", Mock.Of<ILogger>());

            dataAccessMock.Verify(da => da.ReadEventForDateFromDBAsync(It.IsAny<DateTime>()), Times.Once);
            dataAccessMock.Verify(da => da.InsertIntoDBAsync(It.IsAny<BsonDocument>()), Times.Once);

            Assert.Single(insertedDocument["workshops"] as BsonArray);
        }

        [Fact]
        public void ConvertWorkshopToBson()
        {
            var workshop = new Workshop {
                begintime = "13:45",
                endtime = "15:45",
                description = "TestDescription *with* markup",
                mentors = new List<string>() { "Foo", "Bar" },
                prerequisites = "TestPrerequisites",
                targetAudience = "TestAudience",
                title = "Test",
                zoomUser = "Test",
                zoom = "Test"
            };

            var bsonDocument = workshop.ToBsonDocument(new DateTime(2010, 1, 1));

            Assert.Equal(new DateTime(2010, 1, 1, 13, 45, 0).ToString("o"), bsonDocument["begintime"]);
            Assert.Equal(new DateTime(2010, 1, 1, 15, 45, 0).ToString("o"), bsonDocument["endtime"]);
            Assert.Equal(workshop.description, bsonDocument["description"]);
            Assert.Equal(new BsonArray(workshop.mentors), bsonDocument["mentors"]);
            Assert.Equal(workshop.prerequisites, bsonDocument["prerequisites"]);
            Assert.Equal(workshop.targetAudience, bsonDocument["targetAudience"]);
            Assert.Equal(workshop.title, bsonDocument["title"]);
            Assert.Equal(workshop.zoomUser, bsonDocument["zoomUser"]);
            Assert.Equal(workshop.zoom, bsonDocument["zoom"]);
        }
    }
}
