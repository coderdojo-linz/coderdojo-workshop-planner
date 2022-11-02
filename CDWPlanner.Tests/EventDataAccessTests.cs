﻿using CDWPlanner.DTO;

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
        //[Fact]
        //public async Task AddNewEvent()
        //{
        //    var dataAccessMock = new Mock<IDataAccess>();
        //    dataAccessMock.Setup(da => da.ReadEventForDateFromDBAsync(It.IsAny<DateTime>()))
        //        .Returns(Task.FromResult<Event>(null));
        //    BsonArray insertedDocument = null;
        //    dataAccessMock.Setup(da => da.InsertIntoDBAsync(It.IsAny<DateTime>(), It.IsAny<BsonArray>()))
        //        .Callback<DateTime, BsonArray>((_, arr) => insertedDocument = arr);

        //    var planZoomMeetingMock = new Mock<IPlanZoomMeeting>();
        //    planZoomMeetingMock.Setup(z => z.CreateZoomMeetingAsync(
        //        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
        //        It.IsAny<string>(), It.IsAny<string>()))
        //        .Returns(Task.FromResult(new Meeting { join_url = "Dummy" }));

        //    var discordBotMock = new Mock<IDiscordBotService>();

        //    //discordBotMock.Setup(d => d.BuildBotMessage(
        //    //    It.IsAny<Workshop>(), It.IsAny<Event>(), It.IsAny<Meeting>(), It.IsAny<DateTime>()))
        //    //    .Returns("Test");

        //    var func = new PlanEvent(dataAccessMock.Object, discordBotMock.Object, null, planZoomMeetingMock.Object, null, null, null, null, null, null);
        //    await func.WriteEventToDB(@"
        //    {
        //      ""Operation"": ""added"",
        //      ""FolderInfo"": {
        //        ""FullFolder"": ""2020-07-17/PLAN.yml"",
        //        ""DateFolder"": ""2020-07-17"",
        //        ""File"": ""PLAN.yml""
        //      },
        //      ""Workshops"": {
        //        ""workshops"": [
        //          {
        //            ""begintime"": ""13:45"",
        //            ""endtime"": ""15:45"",
        //            ""status"": ""Published"",
        //            ""title"": ""Test"",
        //            ""targetAudience"": ""TestAudience"",
        //            ""description"": ""TestDescription *with* markup"",
        //            ""prerequisites"": ""TestPrerequisites"",
        //            ""mentors"": [ ""Foo"", ""Bar"" ],
        //            ""zoomUser"": ""Test"",
        //            ""zoom"": ""Test"",
        //            ""shortCode"": ""Test""
        //          }
        //        ]
        //      }
        //    }", Mock.Of<ILogger>());

        //    dataAccessMock.Verify(da => da.ReadEventForDateFromDBAsync(It.IsAny<DateTime>()), Times.Once);
        //    dataAccessMock.Verify(da => da.InsertIntoDBAsync(It.IsAny<DateTime>(), It.IsAny<BsonArray>()), Times.Once);

        //    Assert.Single(insertedDocument);
        //}

        [Fact]
        public void ConvertWorkshopToBson()
        {
            var workshop = new Workshop
            {
                begintime = "13:45",
                endtime = "15:45",
                description = "TestDescription *with* markup",
                mentors = new List<string>() { "Foo", "Bar" },
                prerequisites = "TestPrerequisites",
                targetAudience = "TestAudience",
                title = "Test",
                zoomUser = "Test",
                zoom = "Test",
                shortCode = "Test",
                zoomShort = new Model.ShortenedLink()
                {
                    AccessKey = "test",
                    Id = "",
                    ShortLink = "",
                    Url = ""
                },
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
            Assert.Equal(workshop.shortCode, bsonDocument["zoom"]);
        }
    }
}