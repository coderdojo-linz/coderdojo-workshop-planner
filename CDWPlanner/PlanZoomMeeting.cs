using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public interface IPlanZoomMeeting
    {
        Task<Meeting> CreateZoomMeetingAsync(string time, string description, string shortCode, string title, string userId, string date, string userID);
        Meeting GetExistingMeeting(IEnumerable<Meeting> existingMeetingBuffer, string shortCode);
        Task<IEnumerable<Meeting>> GetExistingMeetingsAsync();
        void UpdateMeetingAsync(Meeting meeting, string time, string description, string shortCode, string title, string userId, string date);
    }

    public class PlanZoomMeeting : IPlanZoomMeeting
    {
        private readonly HttpClient client;

        public PlanZoomMeeting(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient("zoom");
        }

        public async Task<IEnumerable<Meeting>> GetExistingMeetingsAsync()
        {
            var meetingsDetails = new List<Meeting>();
            for (var userNum = 0; userNum < 4; userNum++)
            {
                var userId = $"zoom0{userNum % 4 + 1}@linz.coderdojo.net";
                var meetingsList = await GetFromZoomAsync<MeetingsRoot>($"users/{userId}/meetings?type=scheduled");
                foreach (var m in meetingsList.meetings)
                {
                    var meeting = await GetFromZoomAsync<Meeting>($"meetings/{m.id}");
                    meetingsDetails.Add(meeting);
                }
            }

            return meetingsDetails;
        }

        public Meeting GetExistingMeeting(IEnumerable<Meeting> existingMeetingBuffer, string shortCode) =>
            existingMeetingBuffer.FirstOrDefault(meeting =>
                meeting.agenda.Contains($"Shortcode: {shortCode}") && meeting.topic.StartsWith("CoderDojo Online: "));
     
        private async Task<T> GetFromZoomAsync<T>(string url)
        {
            using var getResponse = await client.GetAsync(url);
            var getJsonContent = getResponse.Content.ReadAsStringAsync().Result;
            return JsonSerializer.Deserialize<T>(getJsonContent);
        }

        public async void UpdateMeetingAsync(Meeting meeting, string time, string description, string shortCode, string title, string userId, string date)
        {
            var zoomUrl = $"meetings/{meeting.id}";
            var startTime = $"{date}T{time}:00";

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl, UriKind.Relative),
                Method = HttpMethod.Patch,
                Content = CreateStringContentForMeeting(description, shortCode, title, userId, startTime, meeting.password)
            };

            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();
        }

        private static StringContent CreateStringContentForMeeting(string description, string shortCode, string title, string userId, string startTime, string randomPsw) =>
            new StringContent(
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
                }), Encoding.UTF8, "application/json");

        public async Task<Meeting> CreateZoomMeetingAsync(string time, string description, string shortCode, string title, string userId, string date, string userID)
        {
            var zoomUrl = $"users/{userID}/meetings";
            var startTime = $"{date}T{time}:00";
            var randomPsw = CreateRandomPassword(10);

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl, UriKind.Relative),
                Method = HttpMethod.Post,
                Content = CreateStringContentForMeeting(description, shortCode, title, userId, startTime, randomPsw)
            };

            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();
            var getJsonContent = getResponse.Content.ReadAsStringAsync().Result;
            return JsonSerializer.Deserialize<Meeting>(getJsonContent);
        }

        private string CreateRandomPassword(int length)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

            for (int i = 0; i < length; i++)
            {
                builder.Append(chars[random.Next(chars.Length)]);
            }

            return builder.ToString();
        }
    }
}
