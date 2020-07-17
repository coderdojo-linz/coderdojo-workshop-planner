using CDWPlaner;
using CDWPlaner.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
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

            var factory = Mock.Of<IHttpClientFactory>();
            var logger = Mock.Of<ILogger>();

            var fileReader = new Mock<IGitHubFileReader>();
            fileReader.Setup(fr => fr.GetYMLFileFromGitHub(It.IsAny<FolderFileInfo>(), It.IsAny<IEnumerable<string>>()))
                .Returns(Task.FromResult(new WorkshopsRoot()))
                .Verifiable();

            var planEvent = new PlanEvent(factory, fileReader.Object);
            var result = await planEvent.ReceiveFromGitHub(githubWebhookRequest.HttpRequestMock.Object, collector.Object, logger);

            Assert.IsType<AcceptedResult>(result);
            collector.Verify(c => c.Add(It.IsAny<WorkshopOperation>()), Times.Once);
            fileReader.Verify(fr => fr.GetYMLFileFromGitHub(It.IsAny<FolderFileInfo>(), It.IsAny<IEnumerable<string>>()), Times.Once);
            Assert.NotNull(operation);
            Assert.Equal("PLAN.yml", operation.FolderInfo.File);
            Assert.Equal("2020-07-17", operation.FolderInfo.DateFolder);
            Assert.Equal("2020-07-17/PLAN.yml", operation.FolderInfo.FullFolder);
            Assert.Equal("modified", operation.Operation);
        }
    }
}
