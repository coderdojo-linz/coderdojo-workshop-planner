using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public interface IPlanZoomMeeting
    {
        Task<Meeting> CreateZoomMeetingAsync(string time, string description, string shortCode, string title, string userId, string date, string userID);
        Meeting GetExistingMeetingAsync(string shortCode, List<Meeting> existingMeetingBuffer);
        Task<List<Meeting>> GetExistingMeetingBufferAsync(string userId);
        void UpdateMeetingAsync(Meeting meeting, string time, string description, string shortCode, string title, string userId, string date);
    }

    public class PlanZoomMeeting : IPlanZoomMeeting
    {
        private readonly HttpClient client;

        public PlanZoomMeeting(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient();
        }

        public async Task<List<Meeting>> GetExistingMeetingBufferAsync(string userId)
        {
            var meetingsList = await ListMeetingsAsync(userId);
            var meetingsDetails = new List<Meeting>();

            foreach (var m in meetingsList.meetings)
            {
                long meetingId = m.id;
                var meeting = await GetMeetingsAsync(meetingId);
                meetingsDetails.Add(meeting);
            }
            return meetingsDetails;
        }

        public Meeting GetExistingMeetingAsync(string shortCode, List<Meeting> existingMeetingBuffer)
        {
            var meetingExist = new List<Meeting>();
            var j = -1;

            for (int i = 0; i < existingMeetingBuffer.Count; i++)
            {

                if (existingMeetingBuffer[i].agenda.Contains($"Shortcode: {shortCode}") && existingMeetingBuffer[i].topic.StartsWith("CoderDojo Online: "))
                {
                    meetingExist.Add(existingMeetingBuffer[i]);
                    j++;
                    return meetingExist[j];
                }
            }
            return default;
        }

        public async void UpdateMeetingAsync(Meeting meeting, string time, string description, string shortCode, string title, string userId, string date)
        {
            var meetingId = meeting.id;
            var zoomUrl = $"https://api.zoom.us/v2/meetings/{meetingId}";
            var zoomToken = Environment.GetEnvironmentVariable("ZOOMTOKEN", EnvironmentVariableTarget.Process);
            var startTime = $"{date}T{time}:00Z";

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl),
                Method = HttpMethod.Patch,
                Headers = {

                    { "meetingId", $"{meetingId}" },
                    { HttpRequestHeader.Authorization.ToString(), $"Bearer {zoomToken}" },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'" },
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000" },
                },
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        topic = $"CoderDojo Online: {title}",
                        start_time = $"{startTime}",
                        schedule_for = $"{userId}",
                        agenda = $"{description}\n\nShortcode: {shortCode}",
                    }), Encoding.UTF8, "application/json")
            };

            using var getResponse = await client.SendAsync(meetingRequest);
        }

        public async Task<MeetingsRoot> ListMeetingsAsync(string userId)
        {
            var zoomToken = Environment.GetEnvironmentVariable("ZOOMTOKEN", EnvironmentVariableTarget.Process);
            var zoomUrl = $"https://api.zoom.us/v2/users/{userId}/meetings";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl),
                Method = HttpMethod.Get,
                Headers = {
                    { "type", "scheduled"},
                    { "userId", $"{userId}"},
                    { "Authorization", $"Bearer {zoomToken}" },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000"},
                },
            };
            using var getResponse = await client.SendAsync(webGetRequest);

            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<MeetingsRoot>(getJsonContent);
        }

        public async Task<Meeting> GetMeetingsAsync(long id)
        {
            var zoomToken = Environment.GetEnvironmentVariable("ZOOMTOKEN", EnvironmentVariableTarget.Process);
            var zoomUrl = $"https://api.zoom.us/v2/meetings/{id}";

            var webGetRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl),
                Method = HttpMethod.Get,
                Headers = {
                    { "meetingid", $"{id}" },
                    { HttpRequestHeader.Authorization.ToString(), $"Bearer {zoomToken}" },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'"},
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000"},
                },
            };
            using var getResponse = await client.SendAsync(webGetRequest);

            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;

            return JsonSerializer.Deserialize<Meeting>(getJsonContent);
        }

        public async Task<Meeting> CreateZoomMeetingAsync(string time, string description, string shortCode, string title, string userId, string date, string userID)
        {
            var zoomUrl = $"https://api.zoom.us/v2/users/{userID}/meetings";
            var zoomToken = Environment.GetEnvironmentVariable("ZOOMTOKEN", EnvironmentVariableTarget.Process);
            var startTime = $"{date}T{time}:00";
            var randomPsw = CreateRandomPassword(10);

            var stringContent =
                    JsonSerializer.Serialize(new
                    {
                        topic = $"CoderDojo Online: {title}",
                        type = "2",
                        start_time = $"{startTime}",
                        duration = "120",
                        schedule_for = $"{userId}",
                        timezone = $"Europe/Vienna",
                        password = randomPsw,
                        agenda = $"{description}\n\nShortcode: {shortCode}",
                    });

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl),
                Method = HttpMethod.Post,
                Headers = {

                    { "userId", $"{userID}" },
                    { HttpRequestHeader.Authorization.ToString(), $"Bearer {zoomToken}" },
                    { HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'" },
                    { HttpRequestHeader.Accept.ToString(), "application/json" },
                    { "Timeout", "1000000000" },
                },
                Content = new StringContent(stringContent, Encoding.UTF8, "application/json")
            };

            using var getResponse = await client.SendAsync(meetingRequest);

            var getContent = getResponse.Content;
            var getJsonContent = getContent.ReadAsStringAsync().Result;
            var jsonResult = JsonSerializer.Deserialize<Meeting>(getJsonContent);
            return jsonResult;
        }
        internal string CreateRandomPassword(int length)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

            for (int i = 0; i < length; i++)
            {
                builder.Append(chars[random.Next(chars.Length)]);
            }

            return builder.ToString();
        }
    }
}
