using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using CDWPlanner.Constants;
using CDWPlanner.Helpers;
using CDWPlanner.Model;
using Discord;
using Discord.Rest;

namespace CDWPlanner
{
    public interface IDiscordBotService
    {
        //Task SendDiscordBotMessage(string msg);

        //string BuildBotMessage(Workshop currentWS, Event cdEvent, Meeting existingMeeting, DateTime date);
        Task<DiscordMessage> SendDiscordBotMessage(Workshop currentWS, Workshop cdEvent, DateTime date,
            Meeting existingMeeting);

        Task NotifyWorkshopBeginsAsync(Workshop workShop);
    };

    public class DiscordBotService : IDiscordBotService
    {
        private SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);

        private readonly IDiscordClient _discordClient;
        private readonly DiscordSettings _settings;
        private readonly ILogger _logger;

        public DiscordBotService
        (
            IDiscordClient discordClient,
            DiscordSettings settings,
            ILogger logger
        )
        {
            _discordClient = discordClient;
            this._settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// TODO: Maybe trigger through a one-time triggered func?
        /// </summary>
        /// <returns></returns>
        private async Task EnsureClientLoggedIn()
        {
            if (!(_discordClient is BaseDiscordClient baseDiscord))
            {
                return;
            }

            if (baseDiscord.LoginState != LoginState.LoggedIn)
            {
                await _initializationSemaphore.WaitAsync();
                try
                {
                    if (baseDiscord.LoginState == LoginState.LoggedIn)
                    {
                        return;
                    }
                    await baseDiscord.LoginAsync(TokenType.Bot, _settings.Token, true);
                }
                finally
                {
                    _initializationSemaphore.Release();
                }
            }
        }

        private readonly Emoji ThumbsUpEmote = new Emoji("\U0001F44D");

        public async Task<DiscordMessage> SendDiscordBotMessage
        (
            Workshop currentWorkshop,
            Workshop dbWorkshop,
            DateTime date,
            Meeting existingMeeting
        )
        {
            Embed embedMessageToSend;
            await EnsureClientLoggedIn();

            var discordMessage = GetDiscordMessage(dbWorkshop);
            var messageContext = await DiscordMessageContext.CreateAsync(_discordClient, discordMessage);

            // We dont have any record in the db yet, so we cannot know wether there is a msg or not
            if (dbWorkshop == null || messageContext.UserMessage == null)
            {
                embedMessageToSend = BuildEmbed(currentWorkshop, date, true);
                await messageContext.SendAsync(embedMessageToSend);
                await messageContext.AddReactionAsync(ThumbsUpEmote);
                return messageContext.Message;
            }

            if (!WorkshopChanged(dbWorkshop, currentWorkshop, existingMeeting))
            {
                return messageContext.Message;
            }

            embedMessageToSend = BuildEmbed(currentWorkshop, date, false);
            await messageContext.UpdateAsync(x => x.Embed = new Optional<Embed>(embedMessageToSend));
            await ResetReactionsIfNecessary(currentWorkshop, ThumbsUpEmote, dbWorkshop, messageContext.UserMessage);

            return discordMessage;
        }

        public async Task NotifyWorkshopBeginsAsync(Workshop workShop)
        {
            await EnsureClientLoggedIn();
            var discordMessage = GetDiscordMessage(workShop);
            var messageContext = await DiscordMessageContext.CreateAsync(_discordClient, discordMessage);
            if (messageContext.UserMessage == null)
            {
                return;
            }

            var mentions = await messageContext.UserMessage.GetReactionUsersAsync(ThumbsUpEmote, 1000)
                .SelectMany(x => x.ToAsyncEnumerable())
                .Where(x => !x.IsBot && x.Id != _discordClient.CurrentUser.Id)
                .Select(x => x.GetMentionString())
                .ToListAsync();

            if (!mentions.Any())
            {
                return;
            }

            var guild = await _discordClient.GetGuildAsync(_settings.GuildId);
            var channel = await guild.GetTextChannelAsync(_settings.ChannelId);

            await channel.SendMessageAsync($"Aufgepasst! Der Workshop {workShop.title} beginnt in Kürze! Link: {workshop.zoomShort?.ShortLink ?? workshop.zoom}\n{string.Join(" ", mentions)}");
        }

        private DiscordMessage GetDiscordMessage(Workshop dbWorkshop)
        {
            var discordMessage = dbWorkshop?.discordMessage?.Clone() ?? new DiscordMessage();
            discordMessage.GuildId ??= _settings.GuildId; // coderdojo austria
            discordMessage.ChannelId ??= _settings.ChannelId; // bot-spam
            return discordMessage;
        }

        private async Task ResetReactionsIfNecessary(Workshop currentWorkshop, Emoji thumbsUpEmote, Workshop dbWorkshop, IUserMessage message)
        {
            if (dbWorkshop == null)
            {
                return;
            }

            if (!WorkshopHelpers.TimeHasChanged(dbWorkshop, currentWorkshop))
            {
                return;
            }

            var hadOneUser = false;

            var reactedUsers = message.GetReactionUsersAsync(thumbsUpEmote, 1000)
                .SelectMany(x => x.ToAsyncEnumerable())
                .Where(x => !x.IsBot && x.Id != _discordClient.CurrentUser.Id);

            await foreach (var reactedUser in reactedUsers)
            {
                hadOneUser = true;
                // Notify user that time has changed
                var affectedTimes = new string[]
                {
                    !WorkshopHelpers.BeginTimeChanged(dbWorkshop, currentWorkshop) ? string.Empty : "Startzeit",
                    !WorkshopHelpers.EndTimeChanged(dbWorkshop, currentWorkshop) ? string.Empty : "Endzeit"
                }.Where(x => !string.IsNullOrEmpty(x));

                var affectedTimeString = string.Join(" und ", affectedTimes);
                try
                {
                    await reactedUser.SendMessageAsync
                    (
                        $"Die {affectedTimeString} vom Workshop **{currentWorkshop.title}** wurde geändert. \n" +
                        $"Er beginnt um **{currentWorkshop.begintime}** und endet um {currentWorkshop.endtime}.:alarm_clock:\n" +
                        $"Deshalb wurde deine Benachrichtigung deaktiviert. Bitte reagiere erneut mit \U0001F44D auf die Nachricht am Server, damit du rechtzeitig erinnert wirst!"
                    );
                }
                catch (Exception)
                {
                    //Ignore
                }
            }

            if (hadOneUser)
            {
                await message.RemoveAllReactionsAsync();
                await message.AddReactionAsync(thumbsUpEmote);
            }
        }

        private async Task<DiscordMessageContext> TryGetDiscordUserMessage(DiscordMessage discordMessage)
        {
            return await DiscordMessageContext.CreateAsync(_discordClient, discordMessage);
        }

        private Embed BuildEmbed(Workshop workshop, DateTime date, bool isNew)
        {
            var eb = new EmbedBuilder()
                .WithTitle(workshop.title)
                .WithDescription(workshop.description)
                .AddField("Datum", $"{date:dd.MM.yyyy}", true)
                .AddField("Zeit", $"{workshop.begintimeAsShortTime}-{workshop.endtimeAsShortTime}", true)
                .AddField(workshop.mentors.Count > 1 ? "Mentors" : "Mentor", GetMentorsText(workshop.mentors), true);

            var meetingLink = workshop.zoomShort?.ShortLink ?? workshop.zoom;
            if (!string.IsNullOrEmpty(meetingLink))
            {
                eb.AddField("Zoom", meetingLink);
            }

            eb = eb.WithThumbnailUrl(GetDefaultThumbnail(workshop.title)) // TODO: overwrite by yaml
                .WithColor(Color.Red)
                .WithUrl($"https://linz.coderdojo.net/termine/#{workshop.shortCode}") //TODO: Implement direct navigation support on site
                .WithFooter(x => x.WithText("Reagiere mit \U0001F44D, um benachrichtigt zu werden")); // Thumbsup

            // Thumbnail default idee:
            //var emotes = new Dictionary<string, string>
            //{
            //    { "scratch", ":smiley_cat: :video_game:" },
            //    { "elektronik", ":bulb: :tools:" },
            //    { "android", ":iphone: :computer:" },
            //    { "hacker", ":man_detective: :woman_detective:" },
            //    { "space", ":rocket: :ringed_planet:" },
            //    { "python", ":snake: :video_game:" },
            //    { "development", ":man_technologist: :woman_technologist:" },
            //    { "javascript", ":desktop: :art:" },
            //    { "webseite", ":desktop: :art:" },
            //    { "css", ":desktop: :art:" },
            //    { "discord", ":space_invader: :robot:" },
            //    { "c#", ":musical_score: :eyeglasses:" },
            //    { "unity", ":crossed_swords: :video_game:" },
            //    { "micro:bit", ":zero: :one:" },
            //    { "java", ":ghost: :clown:" },
            //};

            if (isNew)
            {
                eb = eb
                    .WithAuthor(x => x.WithName("Neu"))
                    .WithColor(Color.Green);
            }
            else
            {
                eb = eb
                    .WithAuthor(x => x.WithName("Geändert"))
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();
            }

            return eb.Build();

            string GetMentorsText(List<string> mentors)
            {
                if (mentors.Count <= 1)
                {
                    return mentors.FirstOrDefault() ?? string.Empty;
                }

                var mentorsFormatted = mentors.Select(x => $"• {x}");
                return string.Join('\n', mentorsFormatted);
            }

            // Todo: extend a bit
            string GetDefaultThumbnail(string title)
            {
                var pairs = new Dictionary<string, string>
                {
                    { "scratch", "https://de.scratch-wiki.info/w/images/e/ed/Scratch_cat_large.png" },
                    { "c#", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/C_Sharp_wordmark.svg/1280px-C_Sharp_wordmark.svg.png" },
                    { "csharp", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/C_Sharp_wordmark.svg/1280px-C_Sharp_wordmark.svg.png" },
                    { "docker", "https://oneclick-cloud.com/wp-content/uploads/2019/09/Bigstock_-139961875-Docker-Emblem.-A-Blue-Whale-With-Several-Containers.-e1574090673987-768x426.jpg" },
                    { "typescript", "https://miro.medium.com/max/816/1*mn6bOs7s6Qbao15PMNRyOA.png" },
                    { "elektronikbasteln", "https://www.ingenieur.de/wp-content/uploads/2020/12/panthermedia_B24073749_1000x667-313x156.jpg" },
                    { "css", "https://www.pngitem.com/pimgs/m/198-1985012_transparent-css3-logo-png-css-logo-transparent-background.png" },
                };

                foreach (var pair in pairs)
                {
                    if (title.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }

                return "https://yt3.ggpht.com/ytc/AAUvwniyiRksrFMPSTrM9xBHSj_uw6vi5unadcUA4qXg=s176-c-k-c0x00ffffff-no-rj"; // coderdojo
            }
        }

        public bool WorkshopChanged(Workshop wsFromDB, Workshop currentWS, Meeting existingMeeting)
        {
            return WorkshopHelpers.WorkshopChanged(wsFromDB, currentWS)
                   || currentWS.status == "Scheduled" && existingMeeting == null;
        }

        public class DiscordMessageContext
        {
            private readonly IDiscordClient _client;

            public DiscordMessage Message { get; }
            public IUserMessage UserMessage { get; private set; }
            public ITextChannel Channel { get; private set; }

            public IGuild Server { get; private set; }

            private DiscordMessageContext
            (
                IDiscordClient client,
                DiscordMessage message,
                IGuild server,
                ITextChannel channel,
                IUserMessage userMessage
            )
            {
                _client = client;
                Message = message.Clone();
                Server = server;
                Channel = channel;
                UserMessage = userMessage;
            }

            public static async Task<DiscordMessageContext> CreateAsync(IDiscordClient client, DiscordMessage message)
            {
                message = (message ?? throw new ArgumentNullException("message cannot be null")).Clone();

                var server = await client.GetGuildAsync(message.GuildId ?? throw new ArgumentException("This is impossible"));
                var channel = await server.GetTextChannelAsync(message.ChannelId ?? throw new ArgumentException("This is impossible"));
                var msg = message.MessageId == null ? null : await channel.GetMessageAsync(message.MessageId.Value) as IUserMessage;
                if (msg == null)
                {
                    message.MessageId = null;
                }

                return new DiscordMessageContext(client, message, server, channel, msg);
            }

            public async Task SendAsync(Embed embed) => await SendAsync(string.Empty, embed);

            public async Task SendAsync(string text = null, Embed embed = null)
            {
                var msg = await Channel.SendMessageAsync(text, embed: embed);
                UserMessage = msg;
                Message.MessageId = msg.Id;
            }

            public async Task UpdateAsync(Action<MessageProperties> action)
            {
                await UserMessage.ModifyAsync(action);
            }

            public async Task AddReactionAsync(Emoji emote)
            {
                await UserMessage.AddReactionAsync(emote);
            }
        }
    }

    public static class DiscordBotExtensions
    {
        public static string GetMentionString(this IUser user) => $"<@!{user.Id}>";
    }
}
