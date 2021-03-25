using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using Microsoft.Azure.WebJobs.ServiceBus;
using CDWPlanner.DTO;
using System.Text;
using System.Threading;
using CDWPlanner.Constants;
using CDWPlanner.Model;
using CDWPlanner.Services;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace CDWPlanner
{
    public class PlanEvent
    {
        private readonly IGitHubFileReader fileReader;
        private readonly IDataAccess dataAccess;
        private readonly IPlanZoomMeeting planZoomMeeting;
        private readonly IDiscordBotService discordBot;
        private readonly NewsletterHtmlBuilder htmlBuilder;
        private readonly ReminderService _reminderService;
        private readonly LinkShortenerService _linkShortenerService;
        private readonly LinkShortenerSettings _linkShortenerSettings;
        private readonly ILogger<PlanEvent> _logger;
        private readonly EmailContentBuilder emailBuilder;

        public PlanEvent
        (
            IDataAccess dataAccess,
            IDiscordBotService discordBot,
            IGitHubFileReader fileReader,
            IPlanZoomMeeting planZoomMeeting,
            EmailContentBuilder emailBuilder,
            NewsletterHtmlBuilder htmlBuilder,
            ReminderService reminderService,
            LinkShortenerService linkShortenerService,
            LinkShortenerSettings linkShortenerSettings,
            ILogger<PlanEvent> logger
        )
        {
            this.fileReader = fileReader;
            this.dataAccess = dataAccess;
            this.planZoomMeeting = planZoomMeeting;
            this.htmlBuilder = htmlBuilder;
            _reminderService = reminderService;
            _linkShortenerService = linkShortenerService;
            _linkShortenerSettings = linkShortenerSettings;
            _logger = logger;
            this.emailBuilder = emailBuilder;
            this.discordBot = discordBot;
        }

        [FunctionName("AddGitHubContent")]
        public async Task<IActionResult> ReceiveFromGitHub(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [ServiceBus("workshopupdate", Connection = "ServiceBusConnection", EntityType = EntityType.Topic)] ICollector<WorkshopOperation> collector,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var gitHubDataObject = JsonSerializer.Deserialize<GitHubData>(requestBody);
            foreach (var commit in gitHubDataObject.commits)
            {
                // Get folder and file info from latest commit
                static IEnumerable<FolderFileInfo> GetFolderAndFile(IEnumerable<string> items) =>
                    items.Distinct()
                        .Select(item => new { fullFolder = item, splittedFolder = item.Split("/") })
                        .Where(item => item.splittedFolder.Length >= 2)
                        .Where(item => item.splittedFolder[^1].EndsWith(".yml") || item.splittedFolder[^1].EndsWith(".yaml"))
                        .Where(item => Regex.IsMatch(item.splittedFolder[^2], @"^\d{4}-\d{2}-\d{2}$"))
                        .Select(item => new FolderFileInfo { FullFolder = item.fullFolder, DateFolder = item.splittedFolder[^2], File = item.splittedFolder[^1] });

                // Add them to a list, one for modified, one for new folders/files
                var commitListAdded = GetFolderAndFile(commit.added)
                    .Select(c => new WorkshopOperation { Operation = "added", FolderInfo = c });
                var commitListChanged = GetFolderAndFile(commit.modified)
                    .Select(c => new WorkshopOperation { Operation = "modified", FolderInfo = c });

                try
                {
                    // Add the data to the collection
                    foreach (var item in commitListAdded.Concat(commitListChanged))
                    {
                        item.Workshops = await fileReader.GetYMLFileFromGitHub(item.FolderInfo, commit.id);
                        collector.Add(item);
                    }
                }
                catch (Exception)
                {
                    log.LogInformation("Wrong YML Format, check README.md for correct format");
                    return new BadRequestResult();
                }
            }

            return new AcceptedResult();
        }

        // Subscribtion of PlanEvent Topic
        // Writes data to MongoDB
        [FunctionName("WriteEventToDB")]
        public async Task WriteEventToDB
        (
            [ServiceBusTrigger("workshopupdate", "transfer-to-db", Connection = "ServiceBusConnection")] string workshopJson,
            ILogger log
        )
        {
            // Now it's JSON
            var workshopOperation = JsonSerializer.Deserialize<WorkshopOperation>(workshopJson);

            var dateFolder = workshopOperation?.FolderInfo?.DateFolder;

            // modified or added
            var operation = workshopOperation?.Operation;

            // Parse as local date and UTC; we need both
            var parsedDateEvent = DateTime.Parse(dateFolder);
            var parsedUtcDateEvent = DateTime.SpecifyKind(DateTime.Parse(dateFolder), DateTimeKind.Utc);

            // Read event data (including workshops) from database
            var dbEventsFound = await dataAccess.ReadEventForDateFromDBAsync(parsedUtcDateEvent);
            var found = dbEventsFound != null;

            // Get workshops and write it into an array only if draft flag is false
            var workshopData = new BsonArray();

            // Read all existing meetings in an in-memory buffer.
            var existingMeetingBuffer = await planZoomMeeting.GetExistingMeetingsAsync();

            var usersBuffer = await planZoomMeeting.GetUsersAsync();

            // Helper variable for calculating user name.
            // Background: We need to distribute zoom meetings between four zoom users (zoom01-zoom04).
            var userNum = 0;
            var hostKey = string.Empty;

            foreach (var incomingWorkShop in workshopOperation.Workshops.workshops.Where(ws => ws.status != WorkshopStatus.Draft).OrderBy(ws => ws.begintime))
            {
                var dbWorkshop = dbEventsFound?.workshops.FirstOrDefault(dbws => dbws.shortCode == incomingWorkShop.shortCode);

                var userId = $"zoom0{userNum % 4 + 1}@linz.coderdojo.net";
                userNum++;
                incomingWorkShop.zoom = string.Empty;
                incomingWorkShop.zoomUser = string.Empty;

                // Find meeting in meeting buffer
                var existingMeeting = planZoomMeeting.GetExistingMeeting(existingMeetingBuffer, incomingWorkShop.shortCode, parsedDateEvent);

                // Create or update meeting
                if (incomingWorkShop.status == WorkshopStatus.Scheduled)
                {
                    if (existingMeeting != null)
                    {
                        log.LogInformation("Updating Meeting");
                        planZoomMeeting.UpdateMeetingAsync(existingMeeting, incomingWorkShop.begintime, dateFolder, incomingWorkShop.title, incomingWorkShop.description, incomingWorkShop.shortCode, userId);
                        incomingWorkShop.zoom = existingMeeting.join_url;
                        var user = planZoomMeeting.GetUser(usersBuffer, existingMeeting.host_id);
                        incomingWorkShop.zoomUser = user.email;
                    }
                    else
                    {
                        log.LogInformation("Creating Meeting");
                        var getLinkData = await planZoomMeeting.CreateZoomMeetingAsync(incomingWorkShop.begintime, dateFolder, incomingWorkShop.title, incomingWorkShop.description, incomingWorkShop.shortCode, userId);
                        incomingWorkShop.zoom = getLinkData.join_url;
                        incomingWorkShop.zoomUser = userId;
                    }

                    if (dbWorkshop?.zoomShort != null)
                    {
                        if (dbWorkshop.zoomShort.Url == incomingWorkShop.zoom)
                        {
                            incomingWorkShop.zoomShort = dbWorkshop.zoomShort;
                        }
                        else
                        {
                            //dbWorkshop.zoomShort.Id
                            var id = incomingWorkShop.shortCode;
                            var accessKey = dbWorkshop.zoomShort.Id == incomingWorkShop.shortCode
                                ? dbWorkshop.zoomShort.AccessKey
                                : _linkShortenerSettings.AccessKey;

                            incomingWorkShop.zoomShort = await _linkShortenerService.ShortenUrl(id, accessKey, incomingWorkShop.zoom);
                        }
                    }
                    else
                    {
                        // Def. create new

                        incomingWorkShop.zoomShort = await _linkShortenerService.ShortenUrl(incomingWorkShop.shortCode, _linkShortenerSettings.AccessKey, incomingWorkShop.zoom);
                    }
                }

                var messageMetaData = await discordBot.SendDiscordBotMessage(incomingWorkShop, dbWorkshop, parsedDateEvent, existingMeeting);
                incomingWorkShop.discordMessage = messageMetaData;
                await _reminderService.ScheduleCallback(dbWorkshop, parsedDateEvent, incomingWorkShop);

                workshopData.Add(incomingWorkShop.ToBsonDocument(parsedDateEvent));
            }

            // Check wheather a new file exists, create/or modifie it
            if (operation == "added" || found == false)
            {
                await dataAccess.InsertIntoDBAsync(parsedUtcDateEvent, workshopData);
                found = true;
            }
            else if (operation == "modified" || found == true)
            {
                await dataAccess.ReplaceDataOfDBAsync(parsedUtcDateEvent, workshopData);
            }

            log.LogInformation("Successfully written data to db");
        }

        [FunctionName("WakeupTimerCallback")]
        public async Task WakeupTimerCallback
        (
            [ServiceBusTrigger("wakeuptimer", "callback", Connection = "ServiceBusConnection")] string messageRaw,
            ILogger logger
        )
        {
            logger.LogInformation("Received wakeup call!");
            var message = JsonSerializer.Deserialize<CallbackMessage>(messageRaw);
            var dbEventsFound = await dataAccess.ReadEventForDateFromDBAsync(message.Date.Date);

            var workShop = dbEventsFound.workshops.FirstOrDefault(x => x.uniqueStateId == message.UniqueStateId);
            if (workShop == null)
            {
                logger.LogInformation("Received outdated workshop");
                return;
            }

            await discordBot.NotifyWorkshopBeginsAsync(workShop);
        }

        // Get the workshop body array
        [FunctionName("GetDBContentForHtml")]
        public async Task<IActionResult> GetDBContentForHtml(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger _)
        {
            if (!req.Query.ContainsKey("date"))
            {
                return new BadRequestObjectResult("Missing parameter 'date'.");
            }

            var date = req.Query["date"];
            var parsedUtcDateEvent = DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc);
            var dbEventsFound = await dataAccess.ReadEventForDateFromDBAsync(parsedUtcDateEvent);

            var responseMessage = htmlBuilder.BuildNewsletterHtml(dbEventsFound.workshops);

            return new OkObjectResult(responseMessage);
        }

        // Get events
        [FunctionName("events")]
        public async Task<IActionResult> GetEvents(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger _)
        {
            var past = false;
            if (req.Query.ContainsKey("past"))
            {
                var pastString = req.Query["past"].ToString();
                bool.TryParse(pastString, out past);
            }

            var dbEvents = await dataAccess.ReadEventsFromDBAsync(past);
            return new OkObjectResult(dbEvents.OrderBy(events => events.date).ToList());
        }

        [FunctionName("SendEmail")]
        public async Task<IActionResult> SendEmail(
           [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
           ILogger _)
        {
            if (!req.Query.ContainsKey("date"))
            {
                return new BadRequestObjectResult("Missing parameter 'date'.");
            }

            var date = req.Query["date"];

            var parsedUtcDateEvent = DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc);
            var eventFound = await dataAccess.ReadEventForDateFromDBAsync(parsedUtcDateEvent);
            var mentorsFromDB = await dataAccess.ReadMentorsFromDBAsync();

            var usersBuffer = await planZoomMeeting.GetUsersAsync();
            foreach (var w in eventFound.workshops)
            {
                var user = planZoomMeeting.GetUser(usersBuffer, w.zoomUser);
                var result = emailBuilder.BuildEmailAndICSFile(w, user.host_key);
                if (result.MentorName != null)
                {
                    await emailBuilder.BuildAndSendEmail(result, mentorsFromDB);
                }
            }
            return new OkObjectResult("Email wurde erfolgreich verschickt");
        }
    }
}