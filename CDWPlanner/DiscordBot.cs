using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CDWPlanner
{
    public class DiscordBot
    {
        private DiscordSocketClient _client;
        private ILogger log;
        public string Message;

        [FunctionName("SendMessage")]
        public async Task DiscordBotMessageReceiver()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;

            var token = "NzM2MTQ3MTExMDIzNzM4ODkx.XxqkbA.IEbI3N5FSirnTJG9Ji9kYmfDFPM";

            // var token = Environment.GetEnvironmentVariable("NameOfYourEnvironmentVariable");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.MessageReceived += MessageReceived;

            //ulong channel = _client.GetChannel(737991344160636989) as Channel;
            //SendMessageToChannel(channel, msg);
            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
/*
        public async Task MessageReceived(DiscordSocketClient client, string msg)
        {
            try
            {
                var id = 737991344160636989;
                var botChannel = (IGuildChannel)client.GetChannel(737991344160636989);

                if (botChannel == null)
                {
                    log.LogInformation($"Nachricht konnte nich gesendet werden: Kanal {id} nicht gefunden!");
                    return;
                }

                if (botChannel is IMessageChannel messageChannel)
                {
                    log.LogInformation($"Nachricht wird an #{botChannel.Name} - {botChannel.Guild.Name} gesendet.");

                    await messageChannel.SendMessageAsync(msg);
                }
            }
            catch (Exception ex)
            {
                // Guess we dont have this channel anymore ¯\_(ツ)_/¯
                log.LogInformation($"Nachricht konnte nich gesendet werden: {ex}");
            }
        }
        */

        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Content == "!events")
            {
                await message.Channel.SendMessageAsync(Message);
            }
        }
        
    }
}
