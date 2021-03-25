using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace CDWPlanner.Model
{
    public class ShortenedLinkResponse<TResponse>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public TResponse Data { get; set; }
    }

    public class ShortenedLink
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("accessKey")]
        public string AccessKey { get; set; }

        [JsonProperty("shortenedLink")]
        public string ShortLink { get; set; }
    }

    public class LinkShortenerSettings
    {
        public string AccessKey { get; }

        public LinkShortenerSettings(string accessKey)
        {
            AccessKey = accessKey;
        }
    }
}