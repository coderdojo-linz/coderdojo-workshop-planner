using Azure.Core;
using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public interface IPlanZoomMeeting
    {
        Task<Meeting> CreateZoomMeetingAsync(string time, string date, string title, string description, string shortCode, string userId);
        Meeting GetExistingMeeting(IEnumerable<Meeting> existingMeetingBuffer, string shortCode, DateTime date);
        Task<IEnumerable<Meeting>> GetExistingMeetingsAsync();
        void UpdateMeetingAsync(Meeting meeting, string time, string date, string title, string description, string shortCode, string userId);
        Task<IEnumerable<User>> ListUsersAsync();
        User GetUser(IEnumerable<User> usersBuffer, string zoomUser);
    }

    public class PlanZoomMeeting : IPlanZoomMeeting
    {
        private readonly HttpClient client;
        private readonly HttpClient zoomTokenClient;

        public PlanZoomMeeting(IHttpClientFactory clientFactory)
        {
            client = clientFactory.CreateClient("zoom");
            zoomTokenClient = clientFactory.CreateClient("zoomToken");
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

        public Meeting GetExistingMeeting(IEnumerable<Meeting> existingMeetingBuffer, string shortCode, DateTime date) =>
            existingMeetingBuffer.FirstOrDefault(meeting =>
                meeting.agenda != null && meeting.agenda.Contains($"Shortcode: {shortCode}") 
                && meeting.topic.StartsWith("CoderDojo Online: ") 
                && meeting.start_time.Year == date.Year
                && meeting.start_time.Month == date.Month
                && meeting.start_time.Day == date.Day);

        public async Task<IEnumerable<User>> ListUsersAsync()
        {
            var usersDetails = new List<User>();
            var usersListAsync = await GetFromZoomAsync<UsersRoot>($"users");
            foreach (var user in usersListAsync.users)
            {
                var userDetail = await GetFromZoomAsync<User>($"users/{user.id}");
                usersDetails.Add(userDetail);
            }

            return usersDetails;
        }

        public User GetUser(IEnumerable<User> usersBuffer, string zoomUser) =>
            usersBuffer.Where(u => (zoomUser == u.email) ||  (zoomUser == u.id)).FirstOrDefault();

        internal record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

        internal async Task<string> GetAccessToken()
        {
            var zoomAccountId = Environment.GetEnvironmentVariable("ZOOMACCOUNTID", EnvironmentVariableTarget.Process);
            var zoomClientId = Environment.GetEnvironmentVariable("ZOOMCLIENTID", EnvironmentVariableTarget.Process);
            var zoomClientSecret = Environment.GetEnvironmentVariable("ZOOMCLIENTSECRET", EnvironmentVariableTarget.Process);
            var body = $"grant_type=account_credentials&account_id={zoomAccountId}";
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "token")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            tokenRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{zoomClientId}:{zoomClientSecret}"))}");
            var tokenResponse = zoomTokenClient.SendAsync(tokenRequest).Result;
            tokenResponse.EnsureSuccessStatusCode();
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(tokenResponse.Content.ReadAsStream());
            return token.AccessToken;
        }

        private async Task<T> GetFromZoomAsync<T>(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {await GetAccessToken()}");
            using var getResponse = await client.SendAsync(request);
            getResponse.EnsureSuccessStatusCode();
            var getJsonContent = await getResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(getJsonContent);
        }

        public async void UpdateMeetingAsync(Meeting meeting, string time, string date, string title, string description, string shortCode, string userId)
        {
            var zoomUrl = $"meetings/{meeting.id}";
            var startTime = $"{date}T{time}:00";

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl, UriKind.Relative),
                Method = HttpMethod.Patch,
                Content = CreateStringContentForMeeting(startTime, title, description, shortCode, meeting.password, userId)
            };
            meetingRequest.Headers.Add("Authorization", $"Bearer {await GetAccessToken()}");

            using var getResponse = await client.SendAsync(meetingRequest);
            var responseContent = await getResponse.Content.ReadAsStringAsync();
            getResponse.EnsureSuccessStatusCode();
        }

        public async Task<Meeting> CreateZoomMeetingAsync(string time, string date, string title, string description, string shortCode, string userId)
        {
            var zoomUrl = $"users/{userId}/meetings";
            var startTime = $"{date}T{time}:00";
            var randomPsw = CreateRandomPassword(10);

            var meetingRequest = new HttpRequestMessage
            {
                RequestUri = new Uri(zoomUrl, UriKind.Relative),
                Method = HttpMethod.Post,
                Content = CreateStringContentForMeeting(startTime, title, description, shortCode, randomPsw, userId)
            };
            meetingRequest.Headers.Add("Authorization", $"Bearer {await GetAccessToken()}");

            using var getResponse = await client.SendAsync(meetingRequest);
            getResponse.EnsureSuccessStatusCode();
            var getJsonContent = getResponse.Content.ReadAsStringAsync().Result;
            return JsonSerializer.Deserialize<Meeting>(getJsonContent);
        }

        private static StringContent CreateStringContentForMeeting(string startTime, string title, string description, string shortCode, string randomPsw, string userId) =>
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
                    settings = new {host_video = "true", participant_video = "true", audio = "voip", join_before_host = "true"}
                }), Encoding.UTF8, "application/json");

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
