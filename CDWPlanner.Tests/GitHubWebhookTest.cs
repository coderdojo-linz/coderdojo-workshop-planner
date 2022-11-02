using CDWPlanner.DTO;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

using MongoDB.Bson;

using Moq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace CDWPlanner.Tests
{
    public class GitHubWebhookTest
    {
        [Fact]
        public async Task SingleCommitSingleYaml()
        {
            var githubWebhookRequestJson = @"
            {
              ""commits"": [
                {
                  ""id"": ""13c178b8ebe91815e59d44aec2f593570d5d00e3"",
                  ""added"": [
                  ],
                  ""removed"": [
                  ],
                  ""modified"": [
                    ""2020-07-17/PLAN.yml""
                  ]
                }
              ]
            }";

            WorkshopOperation operation = null;
            using var githubWebhookRequest = new MockHttpRequest(githubWebhookRequestJson);
            var collector = new Mock<ICollector<WorkshopOperation>>();
            collector.Setup(c => c.Add(It.IsAny<WorkshopOperation>()))
                .Callback<WorkshopOperation>(wo => operation = wo)
                .Verifiable();

            var logger = Mock.Of<ILogger>();

            var fileReader = new Mock<IGitHubFileReader>();
            fileReader.Setup(fr => fr.GetWorkshopData(It.IsAny<FolderFileInfo>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new WorkshopsRoot()))
                .Verifiable();

            var planEvent = new PlanEvent(null, null, fileReader.Object, null, null, null, null, null, null, null);
            var result = await planEvent.ReceiveFromGitHub(githubWebhookRequest.HttpRequestMock.Object, collector.Object, logger);

            Assert.IsType<AcceptedResult>(result);
            collector.Verify(c => c.Add(It.IsAny<WorkshopOperation>()), Times.Once);
            fileReader.Verify(fr => fr.GetWorkshopData(It.IsAny<FolderFileInfo>(), It.IsAny<string>()), Times.Once);
            Assert.NotNull(operation);
            Assert.Equal("PLAN.yml", operation.FolderInfo.File);
            Assert.Equal("2020-07-17", operation.FolderInfo.DateFolder);
            Assert.Equal("2020-07-17/PLAN.yml", operation.FolderInfo.FullFolder);
            Assert.Equal("modified", operation.Operation);
        }

        [Fact]
        public async Task MultipleCommitsMultipleYamls()
        {
            var githubWebhookRequestJson = @"
            {
              ""commits"": [
                {
                  ""id"": ""13c178b8ebe91815e59d44aec2f593570d5d00e3"",
                  ""added"": [
                  ],
                  ""removed"": [
                  ],
                  ""modified"": [
                    ""2020-07-17/PLAN.yml""
                  ]
                },
                {
                  ""id"": ""13c178b8ebe91815e59d44aec2f593570d5d00f3"",
                  ""added"": [
                    ""2020-07-18/PLAN.yml""
                  ],
                  ""removed"": [
                  ],
                  ""modified"": [
                  ]
                }
              ]
            }";

            var operations = new List<WorkshopOperation>();
            using var githubWebhookRequest = new MockHttpRequest(githubWebhookRequestJson);
            var collector = new Mock<ICollector<WorkshopOperation>>();
            collector.Setup(c => c.Add(It.IsAny<WorkshopOperation>()))
                .Callback<WorkshopOperation>(wo => operations.Add(wo))
                .Verifiable();

            var logger = Mock.Of<ILogger>();

            var fileReader = new Mock<IGitHubFileReader>();
            fileReader.Setup(fr => fr.GetWorkshopData(It.IsAny<FolderFileInfo>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new WorkshopsRoot()))
                .Verifiable();

            var planEvent = new PlanEvent(null, null, fileReader.Object, null, null, null, null, null, null, null);
            var result = await planEvent.ReceiveFromGitHub(githubWebhookRequest.HttpRequestMock.Object, collector.Object, logger);

            Assert.IsType<AcceptedResult>(result);
            collector.Verify(c => c.Add(It.IsAny<WorkshopOperation>()), Times.Exactly(2));
            fileReader.Verify(fr => fr.GetWorkshopData(It.IsAny<FolderFileInfo>(), It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(2, operations.Count);
            Assert.Equal("PLAN.yml", operations[0].FolderInfo.File);
            Assert.Equal("2020-07-17", operations[0].FolderInfo.DateFolder);
            Assert.Equal("2020-07-17/PLAN.yml", operations[0].FolderInfo.FullFolder);
            Assert.Equal("modified", operations[0].Operation);
            Assert.Equal("PLAN.yml", operations[1].FolderInfo.File);
            Assert.Equal("2020-07-18", operations[1].FolderInfo.DateFolder);
            Assert.Equal("2020-07-18/PLAN.yml", operations[1].FolderInfo.FullFolder);
            Assert.Equal("added", operations[1].Operation);
        }

        [Fact]
        public void BuildEventDocument()
        {
            var builtEvent = DataAccess.BuildEventDocument(new DateTime(2020, 12, 31),
                new BsonArray(new[] { "Foo", "Bar" }));

            Assert.Equal(new DateTime(2020, 12, 31), builtEvent["date"]);
            Assert.Equal("CoderDojo Virtual", builtEvent["type"]);
            Assert.Equal("CoderDojo Online", builtEvent["location"]);
            Assert.Equal(new BsonArray(new[] { "Foo", "Bar" }), builtEvent["workshops"]);
        }

        [Fact]
        public void BuildEventDocumentWithoutWorkshops()
        {
            var builtEvent = DataAccess.BuildEventDocument(new DateTime(2020, 12, 31),
                new BsonArray());

            builtEvent["location"] = "CoderDojo Online";
            Assert.True(new BsonArray().Count == 0 || new BsonArray() == null);
            builtEvent["location"] += " - Themen werden noch bekannt gegeben";
        }

        [Fact]
        public void YamlToWorkshopsTest()
        {
            var workshop = @"workshops:
- begintime: '13:45'
  endtime: '15:45'
  status: Published
  title: Virtuelles Elektronikbasteln mit Raspberry Pi
  targetAudience: 'ab 8'
  description: >-
    Test
  prerequisites: >-
    TestTest
  mentors:
    - Günther
  zoom: 'linklink'
- begintime: '12:00'
  endtime: '13:00'
  status: Published
  title: 'Ein Spiel mit Python'
  targetAudience: '8'
  description: >-
     testdes
  prerequisites: >-
     Aktuelle Version von Python
  mentors:
    - Sonja
  zoom: 'link'";

            var getContent = GitHubFileReader.Deserialize(workshop);

            Assert.Equal("13:45", getContent.workshops[0].begintime);
            Assert.Equal("15:45", getContent.workshops[0].endtime);
            Assert.Equal("Published", getContent.workshops[0].status);
            Assert.Equal("Virtuelles Elektronikbasteln mit Raspberry Pi", getContent.workshops[0].title);
            Assert.Equal("ab 8", getContent.workshops[0].targetAudience);
            Assert.Equal("Test", getContent.workshops[0].description);
            Assert.Equal("TestTest", getContent.workshops[0].prerequisites);
            Assert.Equal("Günther", getContent.workshops[0].mentors[0]);
            Assert.Equal("linklink", getContent.workshops[0].zoom);
            Assert.Equal("12:00", getContent.workshops[1].begintime);
            Assert.Equal("13:00", getContent.workshops[1].endtime);
            Assert.Equal("Published", getContent.workshops[0].status);
            Assert.Equal("Ein Spiel mit Python", getContent.workshops[1].title);
            Assert.Equal("8", getContent.workshops[1].targetAudience);
            Assert.Equal("testdes", getContent.workshops[1].description);
            Assert.Equal("Aktuelle Version von Python", getContent.workshops[1].prerequisites);
            Assert.Equal("Sonja", getContent.workshops[1].mentors[0]);
            Assert.Equal("link", getContent.workshops[1].zoom);
        }
    }
}