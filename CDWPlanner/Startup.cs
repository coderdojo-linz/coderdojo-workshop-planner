using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Runtime.CompilerServices;

[assembly: FunctionsStartup(typeof(CDWPlanner.Startup))]
[assembly: InternalsVisibleTo("CDWPlanner.Tests")]

namespace CDWPlanner
{
    class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var githubUser = Environment.GetEnvironmentVariable("GITHUBUSER", EnvironmentVariableTarget.Process);
            builder.Services.AddHttpClient("github", c =>
            {
                c.BaseAddress = new Uri($"https://raw.githubusercontent.com/{githubUser}/");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/json");
                c.DefaultRequestHeaders.Add("Timeout", "1000000000");
            });

            var zoomToken = Environment.GetEnvironmentVariable("ZOOMTOKEN", EnvironmentVariableTarget.Process);
            builder.Services.AddHttpClient("zoom", c =>
            {
                c.BaseAddress = new Uri("https://api.zoom.us/v2/");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {zoomToken}");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/json");
                c.DefaultRequestHeaders.Add("Timeout", "1000000000");
            });

            //var discordChannelUrl = Environment.GetEnvironmentVariable("DISCORDCHANNEL", EnvironmentVariableTarget.Process);
            //builder.Services.AddHttpClient("discord", c =>
            //{
            //    c.BaseAddress = new Uri($"https://discordapp.com/api/webhooks/{discordChannelUrl}");
            //    c.DefaultRequestHeaders.Add(HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'");
            //    c.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/json");
            //    c.DefaultRequestHeaders.Add("Timeout", "1000000000");
            //});

            builder.Services.AddSingleton<IGitHubFileReader, GitHubFileReader>();
            builder.Services.AddSingleton<IPlanZoomMeeting, PlanZoomMeeting>();
            builder.Services.AddSingleton<IDataAccess, DataAccess>();
            builder.Services.AddSingleton<IDiscordBot, DiscordBot>();
            builder.Services.AddSingleton<NewsletterHtmlBuilder>();
            builder.Services.AddSingleton<EmailContentBuilder>();
        }
    }
}
