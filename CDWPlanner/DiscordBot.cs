using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
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
            var info = "Hey :wave:, hier ist ein Update zu einem Online CoderDojo Workshop: :point_down: \n";
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
                    content = $"{info}{msg}"
                }), Encoding.UTF8, "application/json")
            };
            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();

            Debug.WriteLine("Something changed. Check out Discord for more info");
        }

        public string BuildBotMessage(Workshop currentWS, Event cdEvent, Meeting existingMeeting, DateTime date)
        {
            var reaction = ":tada:";
            // Event does not exist yet -> workshop must be new
            if (cdEvent == null)
            {
                return $"Der Workshop **{currentWS.title}** wurde hinzugefügt und startet am **{date:dd.MM.yyyy}** um **{currentWS.begintimeAsShortTime}** Uhr.{reaction}\n";
            }

            var wsFromDB = cdEvent.workshops.FirstOrDefault(dbws => dbws.shortCode == currentWS.shortCode);

            // Workshop does not exist yet
            if (wsFromDB == null)
            {
                return $"Der Workshop **{currentWS.title}** wurde hinzugefügt und startet am **{date:dd.MM.yyyy}** um **{currentWS.begintimeAsShortTime}** Uhr.{reaction}\n";
            }

            // Event is not new and workshop is not new

            if (currentWS.title != wsFromDB.title)
            {
                var emotes = new Dictionary<string, string>
                {
                    { "scratch", ":smiley_cat: :video_game:" },
                    { "elektronik", ":bulb: :tools:" },
                    { "android", ":iphone: :computer:" },
                    { "hacker", ":man_detective: :woman_detective:" },
                    { "space", ":rocket: :ringed_planet:" },
                    { "python", ":snake: :video_game:" },
                    { "development", ":man_technologist: :woman_technologist:" },
                    { "javascript", ":desktop: :art:" },
                    { "webseite", ":desktop: :art:" },
                    { "css", ":desktop: :art:" },
                    { "discord", ":space_invader: :robot:" },
                    { "c#", ":musical_score: :notes:" },
                    { "unity", ":crossed_swords: :video_game:" },
                    { "micro:bit", ":zero: :one:" },
                    { "java", ":ghost: :clown:" },
                };

                var e = emotes.Keys.FirstOrDefault(k => currentWS.title.ToLower().Contains(k));
                if (e != null)
                {
                    reaction = emotes[e];
                }

                return $"Der Titel des Workshops **{wsFromDB.title}** lautet nun **{currentWS.title}**.{reaction}\n";
            }

            if (currentWS.description != wsFromDB.description)
            {
                reaction = ":pencil:";
                return $"Der Workshop **{currentWS.title}** hat nun eine neue Beschreibung.{reaction}\n";
            }

            if (currentWS.begintime != wsFromDB.begintimeAsShortTime)
            {
                reaction = ":alarm_clock:";
                return $"Die Startzeit vom Workshop **{currentWS.title}** wurde geändert. Er beginnt um **{currentWS.begintime}**.{reaction}\n";
            }

            if (currentWS.prerequisites != wsFromDB.prerequisites)
            {
                reaction = ":ballot_box_with_check:";
                return $"Die Workshop Voraussetzungen von **{currentWS.title}** wurden geändert.{reaction}\n";
            }

            if (currentWS.status == "Scheduled" && existingMeeting == null)
            {
                return $"Es gibt nun einen Zoom-Link für den Workshop **{currentWS.title}**: {currentWS.zoom}\n";
            }

            return string.Empty;
        }
    }
}
