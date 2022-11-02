using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Runtime.CompilerServices;
using CDWPlanner.Model;
using CDWPlanner.Services;
using Discord;
using Discord.Rest;
using Azure.Messaging.ServiceBus;
using ShlinkDotnet.Extensions;

[assembly: FunctionsStartup(typeof(CDWPlanner.Startup))]
[assembly: InternalsVisibleTo("CDWPlanner.Tests")]

namespace CDWPlanner
{
    internal class Startup : FunctionsStartup
    {
        public Microsoft.Extensions.Configuration.IConfiguration Configuration { get; private set; }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            Configuration = builder.GetContext().Configuration;
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

            builder.Services.AddHttpClient("linkshortener", c =>
            {
                c.BaseAddress = new Uri("https://meet.coderdojo.net/api/");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.ContentType.ToString(), "application/json;charset='utf-8'");
                c.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/json");
            });

      

            builder.Services.AddSingleton<IGitHubFileReader, GitHubFileReader>();
            builder.Services.AddSingleton<IPlanZoomMeeting, PlanZoomMeeting>();
            builder.Services.AddSingleton<IDataAccess, DataAccess>();
            builder.Services.AddSingleton<NewsletterHtmlBuilder>();
            builder.Services.AddSingleton<EmailContentBuilder>();
            builder.Services.AddTransient<ReminderService>();
            builder.Services.AddTransient<LinkShortenerService>();

            var lsAccessKey = Environment.GetEnvironmentVariable("LINKSHORTENER_ACCESSKEY", EnvironmentVariableTarget.Process);

            builder.Services.AddSingleton(new LinkShortenerSettings(lsAccessKey));

            builder.Services.AddShlink(Configuration.GetSection("shlink"));

            ConfigureDiscordBot(builder);
            ConfigureServiceBus(builder);
        }



        private void ConfigureServiceBus(IFunctionsHostBuilder builder)
        {
            
            //var wakeupTimerConnection = new TopicClient(connection, "WakeupTimer", RetryPolicy.Default);
            builder.Services.AddTransient(sp =>
            {
                var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnection", EnvironmentVariableTarget.Process);
                return new ServiceBusClient(connectionString);
            });
        }

        private void ConfigureDiscordBot(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IDiscordClient>(new DiscordRestClient(new DiscordRestConfig
            {
                DefaultRetryMode = RetryMode.AlwaysRetry
            }));


            

            var discordBotToken = Configuration["DISCORD_BOT_TOKEN"]; // Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN", EnvironmentVariableTarget.Process);
            var rawGuildId = Configuration["DISCORD_BOT_GUILD_ID"];  // Environment.GetEnvironmentVariable("DISCORD_BOT_GUILD_ID", EnvironmentVariableTarget.Process);
            var rawChannelId = Configuration["DISCORD_BOT_CHANNEL_ID"]; // Environment.GetEnvironmentVariable("DISCORD_BOT_CHANNEL_ID", EnvironmentVariableTarget.Process);

            builder.Services.AddSingleton(new DiscordSettings()
            {
                Token = discordBotToken,
                GuildId = ulong.TryParse(rawGuildId, out var guildId) ? guildId : 704990064039559238,
                ChannelId = ulong.TryParse(rawChannelId, out var channelId) ? channelId : 719867879054377092
            });

            builder.Services.AddSingleton<IDiscordBotService, DiscordBotService>();

            //((DiscordBotService)(builder.Services.BuildServiceProvider().GetService<IDiscordBotService>())).SendTestMessage().Wait();
        }
    }
}