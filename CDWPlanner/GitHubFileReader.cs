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
            client = clientFactory.CreateClient();
        }

        // GET request to GitHub to get the YML file data with specific URL
        public async Task<WorkshopsRoot> GetYMLFileFromGitHub(FolderFileInfo info, string commitId)
        {
            var githubUser = Environment.GetEnvironmentVariable("GITHUBUSER", EnvironmentVariableTarget.Process);

            var url = $"https://raw.githubusercontent.com/{githubUser}/{commitId}/{info.FullFolder}";

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
