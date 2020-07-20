using CDWPlaner;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
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
                    ""targetAudience"": ""Testaudience"",
                    ""description"": ""Testdescription *with* markup"",
                    ""prerequisites"": ""Testprerequisites"",
                    ""mentors"": [ ""Foo"", ""Bar"" ],
                    ""zoom"": ""https://us02web.zoom.us/...""
                  }
                ]
              }
            }", Mock.Of<ILogger>());

            dataAccessMock.Verify(da => da.ReadWorkshopForDateAsync(It.IsAny<DateTime>()), Times.Once);
            dataAccessMock.Verify(da => da.InsertIntoDBAsync(It.IsAny<BsonDocument>()), Times.Once);

            Assert.Equal(new BsonDateTime(new DateTime(2020, 7, 17, 0, 0, 0, 0, DateTimeKind.Utc)), insertedDocument["date"]);
        }
    }
}
