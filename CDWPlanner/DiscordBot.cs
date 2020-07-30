using CDWPlanner.DTO;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public interface IDiscordBot
    {
        Task SendDiscordBotMessage(string msg);
        string BuildBotMessage(Workshop currentWS, Event cdEvent, Meeting existingMeeting, DateTime date);
    };

    public class DiscordBot : IDiscordBot
    {
        private readonly HttpClient client;

        public DiscordBot(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient("discord");
        }

        public async Task SendDiscordBotMessage(string msg)
        {
            if (msg == "")
            {
                Debug.WriteLine("Nothing changed");
                return;
            }
            var meetingRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    content = msg
                }), Encoding.UTF8, "application/json")
            };
            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();

            Debug.WriteLine("Something changed. Check out Discord for more info");
        }

        public string BuildBotMessage(Workshop currentWS, Event cdEvent, Meeting existingMeeting, DateTime date)
        {
            // Event does not exist yet -> workshop must be new
            if (cdEvent == null)
            {
                return $"Der Workshop '{currentWS.title}' wurde hinzugefügt und startet am {date:dd.MM.yyyy} um {currentWS.begintimeAsShortTime} Uhr.\n";
            }

            var wsFromDB = cdEvent.workshops.FirstOrDefault(dbws => dbws.shortCode == currentWS.shortCode);

            // Workshop does not exist yet
            if (wsFromDB == null)
            {
                return $"Der Workshop '{currentWS.title}' wurde hinzugefügt und startet am {date:dd.MM.yyyy} um {currentWS.begintimeAsShortTime} Uhr.\n";
            }

            // Event is not new and workshop is not new

            if (currentWS.title != wsFromDB.title)
            {
                return $"Der Titel des *Workshops* '{wsFromDB.title}' lautet nun '{currentWS.title}' :thumbsup:.\n";
            }

            if (currentWS.description != wsFromDB.description)
            {
                return $"Der Workshop '{currentWS.title}' hat nun eine neue Beschreibung.\n";
            }

            if (currentWS.begintime != wsFromDB.begintimeAsShortTime)
            {
                return $"Die Startzeit vom Workshop '{currentWS.title}' wurde geändert. Er beginnt um {currentWS.begintime}.\n";
            }

            if (currentWS.prerequisites != wsFromDB.prerequisites)
            {
                return $"Die Workshop Voraussetzungen von '{currentWS.title}' wurden geändert.\n";
            }

            if (currentWS.status == "Scheduled" && existingMeeting == null)
            {
                return $"Es gibt nun einen Zoom-Link für den Workshop '{currentWS.title}': {currentWS.zoom}\n";
            }

            return string.Empty;
        }
    }
}
