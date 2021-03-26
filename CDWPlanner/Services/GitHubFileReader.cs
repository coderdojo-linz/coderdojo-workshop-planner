using CDWPlanner.DTO;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
            client = clientFactory.CreateClient("github");
        }

        // GET request to GitHub to get the YML file data with specific URL
        public async Task<WorkshopsRoot> GetYMLFileFromGitHub(FolderFileInfo info, string commitId)
        {
            var url = $"{commitId}/{info.FullFolder}";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(url, UriKind.Relative),
                Method = HttpMethod.Get,
            };
            using var getResponse = await client.SendAsync(webGetRequest);
            var getContent = getResponse.Content;
            var getYmlContent = getContent.ReadAsStringAsync().Result;
            return YamlToWorkshops(getYmlContent);
        }

        internal static WorkshopsRoot YamlToWorkshops(string getYmlContent)
        {
            var ymlContent = new StringReader(getYmlContent);

            var deserializer = new DeserializerBuilder().Build();

            var yamlObject = deserializer.Deserialize<WorkshopsRoot>(ymlContent);
            return yamlObject;
        }
    }
}
