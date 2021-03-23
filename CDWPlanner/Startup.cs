using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Runtime.CompilerServices;
using CDWPlanner.Model;
using Discord;
using Discord.Rest;

[assembly: FunctionsStartup(typeof(CDWPlanner.Startup))]
[assembly: InternalsVisibleTo("CDWPlanner.Tests")]

namespace CDWPlanner
{
    internal class Startup : FunctionsStartup
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

            builder.Services.AddSingleton<IGitHubFileReader, GitHubFileReader>();
            builder.Services.AddSingleton<IPlanZoomMeeting, PlanZoomMeeting>();
            builder.Services.AddSingleton<IDataAccess, DataAccess>();
            builder.Services.AddSingleton<NewsletterHtmlBuilder>();
            builder.Services.AddSingleton<EmailContentBuilder>();

            ConfigureDiscordBot(builder);
        }

        private static void ConfigureDiscordBot(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IDiscordClient>(new DiscordRestClient(new DiscordRestConfig
            {
                DefaultRetryMode = RetryMode.AlwaysRetry
            }));

            var discordBotToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN", EnvironmentVariableTarget.Process);
            var rawGuildId = Environment.GetEnvironmentVariable("DISCORD_BOT_GUILD_ID", EnvironmentVariableTarget.Process);
            var rawChannelId = Environment.GetEnvironmentVariable("DISCORD_BOT_CHANNEL_ID", EnvironmentVariableTarget.Process);

            builder.Services.AddSingleton(new DiscordSettings()
            {
                Token = discordBotToken,
                GuildId = ulong.TryParse(rawGuildId, out var guildId) ? guildId : 704990064039559238,
                ChannelId = ulong.TryParse(rawChannelId, out var channelId) ? channelId : 719867879054377092
            });

            builder.Services.AddSingleton<IDiscordBotService, DiscordBotService>();
        }
    }
}