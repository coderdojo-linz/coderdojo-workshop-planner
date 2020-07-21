using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace CDWPlanner
{
    public interface IGitHubFileReader
    {
        Task<WorkshopsRoot> GetYMLFileFromGitHub(FolderFileInfo info, string Id);
    }

    public class GitHubFileReader : IGitHubFileReader
    {
        private readonly HttpClient client;

        public GitHubFileReader(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient();
        }

        // GET request to GitHub to get the YML file data with specific URL
        public async Task<WorkshopsRoot> GetYMLFileFromGitHub(FolderFileInfo info, string commitId)
        {
            var url = $"https://raw.githubusercontent.com/UndeMe/CDWPlanner/{commitId}/{info.FullFolder}";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get,
                Headers = {
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000"},
                },
            };
            using var getResponse = await client.SendAsync(webGetRequest);
            var getContent = getResponse.Content;
            var getYmlContent = getContent.ReadAsStringAsync().Result;

            var ymlContent = new StringReader(getYmlContent);

            var deserializer = new DeserializerBuilder().Build();

            var yamlObject = deserializer.Deserialize<WorkshopsRoot>(ymlContent);
            return yamlObject;
        }
    }
}
