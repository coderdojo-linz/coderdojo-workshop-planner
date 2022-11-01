using CDWPlanner.DTO;
using CDWPlanner.Helpers;

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
        Task<WorkshopsRoot> GetWorkshopData(FolderFileInfo info, string Id);
        Task<T> GetYMLFile<T>(string commitId, string fullFolder);
    }

    public class GitHubFileReader : IGitHubFileReader
    {
        private readonly HttpClient client;

        public GitHubFileReader(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient("github");
        }

        //https://raw.githubusercontent.com/JKamsker/coderdojo-online/4435202c36ca8ea0e5e28ffd526213b6e9e28e14/.config/imageconfig.yml
        // https://raw.githubusercontent.com/JKamsker/coderdojo-online/master/.config/imageconfig.yml
        // GET request to GitHub to get the YML file data with specific URL

        public async Task<WorkshopsRoot> GetWorkshopData(FolderFileInfo info, string commitId)
        {
            return await GetYMLFile<WorkshopsRoot>(commitId, info.FullFolder);
        }
        
        public async Task<T> GetYMLFile<T>(string commitId, string fullFolder)
        {
            var url = $"{commitId}/{fullFolder}";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(url, UriKind.Relative),
                Method = HttpMethod.Get,
            };
            using var getResponse = await client.SendAsync(webGetRequest);
            if (!getResponse.IsSuccessStatusCode)
            {
                return default;
            }
            
            var getContent = getResponse.Content;
            var getYmlContent = getContent.ReadAsStringAsync().Result;
            return Deserialize<T>(getYmlContent);
        }

        internal static T Deserialize<T>(string getYmlContent)
        {
            var ymlContent = new StringReader(getYmlContent);

            var deserializer = new DeserializerBuilder().Build();

            var yamlObject = deserializer.Deserialize<T>(ymlContent);
            return yamlObject;
        }
    }
}
