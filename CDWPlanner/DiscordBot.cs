using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public class DiscordBot
    {
        public string Message;
        private readonly HttpClient client;

        public DiscordBot(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient("zoom");
        }

        public async Task DiscordBotMessageReceiver()
        {
            var discordUrl = @"https://discordapp.com/api/webhooks/738263739391934536/WhTtOIUU-0PDW0HQlNsMAdEF6Q0PrtYagtTLFm6ewU8otPo8SyKNL8CXRaFbBj7v91lP";
            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(discordUrl),
                Method = HttpMethod.Post,
                Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    content = $"{Message}"
                }), Encoding.UTF8, "application/json")
             };
            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();
        }
    }
}
