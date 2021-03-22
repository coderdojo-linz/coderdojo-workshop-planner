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
using CDWPlanner.Constants;
using CDWPlanner.Model;
using Discord;
using Discord.Rest;

namespace CDWPlanner
{
    public interface IDiscordBotService
    {
        //Task SendDiscordBotMessage(string msg);

        //string BuildBotMessage(Workshop currentWS, Event cdEvent, Meeting existingMeeting, DateTime date);
        Task<DiscordMessage> SendDiscordBotMessage(Workshop currentWS, Event cdEvent, DateTime date);
    };

    public class DiscordBotService : IDiscordBotService
    {
        private SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);

        private readonly IDiscordClient _discordClient;

        public DiscordBotService(IDiscordClient discordClient)
        {
            _discordClient = discordClient;
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
                    await baseDiscord.LoginAsync(TokenType.Bot, "", true);
                }
                finally
                {
                    _initializationSemaphore.Release();
                }
            }
        }

        public async Task<DiscordMessage> SendDiscordBotMessage(Workshop currentWorkshop, Event cdEvent, DateTime date)
        {
            await EnsureClientLoggedIn();
            var thumbsUpEmote = new Emoji("\U0001F44D");

            var dbWorkshop = cdEvent?.workshops.FirstOrDefault(dbws => dbws.shortCode == currentWorkshop.shortCode);
            var embedMessageToSend = BuildEmbed(currentWorkshop, date, dbWorkshop == null);

            var discordMessage = dbWorkshop?.discordMessage?.Clone() ?? new DiscordMessage();
            discordMessage.GuildId ??= 704990064039559238; // coderdojo austria
            discordMessage.ChannelId ??= 719867879054377092; // bot-spam

            var message = await CreateOrUpdateMessageAsync(embedMessageToSend, discordMessage);

            var reactionRequired = !message.Reactions.TryGetValue(thumbsUpEmote, out var reaction) || !reaction.IsMe;
            if (reactionRequired)
            {
                await message.AddReactionAsync(thumbsUpEmote);
            }

            await ResetReactionsIfNecessary(currentWorkshop, thumbsUpEmote, dbWorkshop, message);

            return discordMessage;
        }

        private async Task ResetReactionsIfNecessary(Workshop currentWorkshop, Emoji thumbsUpEmote, Workshop dbWorkshop, IUserMessage message)
        {
            if (dbWorkshop == null)
            {
                return;
            }

            var changeEvent = new WorkshopChangedEvent(dbWorkshop, currentWorkshop);
            if (!changeEvent.TimeHasChanged)
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
                    !changeEvent.BeginTimeChanged ? string.Empty : "Startzeit",
                    !changeEvent.EndTimeChanged ? string.Empty : "Endzeit"
                }.Where(x => !string.IsNullOrEmpty(x));

                var affectedTimeString = string.Join(" und ", affectedTimes);

                await reactedUser.SendMessageAsync
                (
                    $"Die {affectedTimeString} vom Workshop **{currentWorkshop.title}** wurde geändert. \n" +
                    $"Er beginnt um **{currentWorkshop.begintime}** und endet um {currentWorkshop.endtime}.:alarm_clock:\n" +
                    $"Deshalb wurde deine Benachrichtigung deaktiviert. Bitte reagiere erneut mit \U0001F44D auf die Nachricht am Server, damit du rechtzeitig erinnert wirst!"
                );
            }

            if (hadOneUser)
            {
                await message.RemoveAllReactionsAsync();
                await message.AddReactionAsync(thumbsUpEmote);
            }
        }

        private async Task<IUserMessage> CreateOrUpdateMessageAsync(Embed embedMessageToSend, DiscordMessage discordMessage)
        {
            var server = await _discordClient.GetGuildAsync(discordMessage.GuildId ?? throw new ArgumentException("This is impossible"));
            var channel = await server.GetTextChannelAsync(discordMessage.ChannelId ?? throw new ArgumentException("This is impossible"));

            IUserMessage message = null;
            if (discordMessage?.MessageId == null)
            {
                // Create new
                return await channel.SendMessageAsync(embed: embedMessageToSend);
            }

            message = await channel.GetMessageAsync(discordMessage.MessageId.Value) as IUserMessage;
            if (message == null)
            {
                //Stored message was not found
                return await channel.SendMessageAsync(embed: embedMessageToSend);
            }

            await message.ModifyAsync(x => x.Embed = new Optional<Embed>(embedMessageToSend));
            return message;
        }

        private Embed BuildEmbed(Workshop workshop, DateTime date, bool isNew)
        {
            var eb = new EmbedBuilder()
                .WithTitle(workshop.title)
                .WithDescription(workshop.description)
                .AddField("Datum", $"{date:dd.MM.yyyy}", true)
                .AddField("Zeit", $"{workshop.begintimeAsShortTime}", true)
                .AddField(workshop.mentors.Count > 1 ? "Mentors" : "Mentor", GetMentorsText(workshop.mentors), true)
                .AddField("Zoom", workshop.zoom) // TODO: Link shortener ("https://meet.coderdojo.net/COOLMEETINGID")
                .WithThumbnailUrl(GetDefaultThumbnail(workshop.title)) // TODO: overwrite by yaml
                .WithColor(Color.Red)
                .WithUrl("https://linz.coderdojo.net/termine/") //TODO: Implement direct navigation support on site
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
                    .WithColor(Color.Blue);
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
    }
}