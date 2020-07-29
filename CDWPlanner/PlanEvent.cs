using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using Microsoft.Azure.WebJobs.ServiceBus;
using CDWPlanner.DTO;
using Markdig;
using System.Text;
using SendGrid;
using SendGrid.Helpers.Mail;
using Ical.Net.Serialization;

namespace CDWPlanner
{
    public class PlanEvent
    {
        private readonly IGitHubFileReader fileReader;
        private readonly IDataAccess dataAccess;
        private readonly IPlanZoomMeeting planZoomMeeting;

        public PlanEvent(IGitHubFileReader fileReader, IDataAccess dataAccess, IPlanZoomMeeting planZoomMeeting)
        {
            this.fileReader = fileReader;
            this.dataAccess = dataAccess;
            this.planZoomMeeting = planZoomMeeting;
        }

        [FunctionName("PlanEvent")]
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

                // Add them to a list, one for modiefied, one for new folders/files
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
        public async Task Receive(
            [ServiceBusTrigger("workshopupdate", "transfer-to-db", Connection = "ServiceBusConnection")] string workshopJson,
            ILogger log)
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

            foreach (var w in workshopOperation.Workshops.workshops.Where(ws => ws.status != "Draft").OrderBy(ws => ws.begintime))
            {
                var userId = $"zoom0{userNum % 4 + 1}@linz.coderdojo.net";
                userNum++;
                w.zoom = string.Empty;
                w.zoomUser = string.Empty;

                // Find meeting in meeting buffer
                var existingMeeting = planZoomMeeting.GetExistingMeeting(existingMeetingBuffer, w.shortCode, parsedDateEvent);

                // Create or update meeting
                if (w.status == "Scheduled")
                {
                    if (existingMeeting != null)
                    {
                        log.LogInformation("Updating Meeting");
                        planZoomMeeting.UpdateMeetingAsync(existingMeeting, w.begintime, w.description, w.shortCode, w.title, userId, dateFolder);
                        w.zoom = existingMeeting.join_url;
                        var user = planZoomMeeting.GetUser(usersBuffer, existingMeeting.host_id);
                        w.zoomUser = user.email;
                    }
                    else
                    {
                        log.LogInformation("Creating Meeting");
                        var getLinkData = await planZoomMeeting.CreateZoomMeetingAsync(w.begintime, w.description, w.shortCode, w.title, userId, dateFolder, userId);
                        w.zoom = getLinkData.join_url;
                        w.zoomUser = userId;
                    }
                }

                workshopData.Add(w.ToBsonDocument(parsedDateEvent));
            }

            // Build object that can be added to DB 
            var eventData = BuildEventDocument(parsedUtcDateEvent, workshopData);

            // Check wheather a new file exists, create/or modifie it
            if (operation == "added" || found == false)
            {
                await dataAccess.InsertIntoDBAsync(eventData);
                found = true;
            }
            else if (operation == "modified" || found == true)
            {
                await dataAccess.ReplaceDataOfDBAsync(parsedUtcDateEvent, eventData);
            }

            log.LogInformation("Successfully written data to db");
        }

        // Build the data for the database
        internal static BsonDocument BuildEventDocument(DateTime parsedDateEvent, BsonArray workshopData)
        {
            var eventData = new BsonDocument();
            eventData.AddRange(new Dictionary<string, object> {
                { "date", parsedDateEvent },
                { "type", "CoderDojo Virtual" },
                { "location", "CoderDojo Online" },
                { "workshops", workshopData}
            });

            if (workshopData == null || workshopData.Count == 0)
            {
                eventData["location"] += " - Themen werden noch bekannt gegeben";
            }
            return eventData;
        }

        // Get the workshop body array
        [FunctionName("GetDBContent")]
        public async Task<IActionResult> Run(
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

            var workshops = dbEventsFound.workshops;
            var responseBuilder = new StringBuilder(@"<section class='main'><table width = '100%'>
                                    <tbody><tr><td>&nbsp;</td><td class='main-td' width='600'>
			                        <h1>Hallo&nbsp;*|FNAME|*,</h1>
			                        <p>Diesen Freitag ist wieder CoderDojo-Nachmittag und es sind viele Workshops im Angebot.Hier eine kurze <strong>Orientierungshilfe</strong>:</p>
                                    ");
            foreach (var w in workshops)
            {
                AddWorkshopHtml(responseBuilder, w);
            }

            responseBuilder.Append(@"</td><td>&nbsp;</td></tr></tbody></table></section>");

            var responseMessage = responseBuilder.ToString();

            return new OkObjectResult(responseMessage);
        }

        // Get events
        [FunctionName("events")]
        public async Task<IActionResult> RunEvent(
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

        [FunctionName("SendEmails")]
        public async Task<IActionResult> SendEmails(
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

            var emailContent = new StringBuilder();

            var icsFileContent = new StringBuilder();
            foreach (var w in eventFound.workshops)
            {
                var firstMentor = await AddWorkshopAndMentorsAsync(emailContent, icsFileContent, w);
                if (firstMentor != null)
                {
                    await SendEmail(emailContent, icsFileContent, mentorsFromDB, firstMentor);
                    emailContent.Clear();
                }
            }

            return new OkObjectResult("Email wurde erfolgreich verschickt");
        }

        private static string ExtractTime(string time) => DateTime.Parse(time).ToString("HH:mm");
        private static string ExtractDate(string time) => DateTime.Parse(time).ToString("yyyyMMddTHHmmss");

        // Build the html string
        internal static void AddWorkshopHtml(StringBuilder responseBuilder, Workshop w)
        {

            var bTime = ExtractTime(w.begintime);
            var eTime = ExtractTime(w.endtime);
            var timeString = $"{bTime} - {eTime}";

            responseBuilder.Append($@"<h3>{w.titleHtml}</h3><p class='subtitle'>{timeString}<br/>{w.targetAudienceHtml}</p><p>{w.descriptionHtml}</p>");
        }

        // Build the email string
        internal async Task<string> AddWorkshopAndMentorsAsync(StringBuilder emailContent, StringBuilder str, Workshop w)
        {
            if (w.mentors.Count == 0)
            {
                return null;
            }

            var usersBuffer = await planZoomMeeting.GetUsersAsync();
            var user = planZoomMeeting.GetUser(usersBuffer, w.zoomUser);

            emailContent.Append($"Hallo {w.mentors[0]}!<br><br>");
            emailContent.Append($"Danke, dass du einen Workshop beim Online CoderDojo anbietest. In diesem Email erhältst du alle Zugangsdaten:<br><br>");
            emailContent.Append($"Titel: {w.titleHtml}<br>Startzeit: {ExtractTime(w.begintime)}<br>Endzeit: {ExtractTime(w.endtime)}<br>Beschreibung: {w.descriptionHtml}");
            emailContent.Append($"<br>Zoom User: {w.zoomUser}<br>Zoom URL: {w.zoom}<br>Dein Hostkey: {user.host_key}<br><br>");
            emailContent.Append($"Viele Grüße,<br>Dein CoderDojo Organisationsteam");

            // Build ics file
            str.AppendLine("BEGIN:VCALENDAR");
            str.AppendLine("VERSION:2.0");
            str.AppendLine("PRODID:-//ical.marudot.com//iCal Event Maker");
            str.AppendLine("CALSCALE:GREGORIAN");
            str.AppendLine("METHOPD:PUBLISH");
            str.AppendLine("CLASS:PUBLIC");
            str.AppendLine("BEGIN:VTIMEZONE");
            str.AppendLine("TZID:Europe/Vienna");
            str.AppendLine("TZURL:http://tzurl.org/zoneinfo-outlook/Europe/Berlin");
            str.AppendLine("X-LIC-LOCATION:Europe/Vienna");
            str.AppendLine("BEGIN:DAYLIGHT");
            str.AppendLine("TZOFFSETFROM:+0100");
            str.AppendLine("TZOFFSETTO:+0200");
            str.AppendLine("TZNAME:CEST");
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", ExtractDate(w.begintime)));
            str.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU");
            str.AppendLine("END:DAYLIGHT");
            str.AppendLine("BEGIN:STANDARD");
            str.AppendLine("TZOFFSETFROM:+0200");
            str.AppendLine("TZOFFSETTO:+0100");
            str.AppendLine("TZNAME:CET");
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", ExtractDate(w.begintime)));
            str.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
            str.AppendLine("END:STANDARD");
            str.AppendLine("END:VTIMEZONE");
            str.AppendLine("BEGIN:VEVENT");
            str.AppendLine("X-WR-RELCALID:XXXXXX");
            str.AppendLine("X-MS-OLK-FORCEINSPECTOROPEN:TRUE");
            str.AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmssZ}", ExtractDate(w.begintime)));
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", ExtractDate(w.begintime)));
            str.AppendLine(string.Format("DTEND:{0:yyyyMMddTHHmmss}", ExtractDate(w.endtime)));
            str.AppendLine(string.Format("SUMMARY:{0}", w.titleHtml));
            str.AppendLine("UID:20200727T072232Z-1947992826@marudot.com");
            str.AppendLine("TZID:Europe/Vienna");
            str.AppendLine(string.Format("DESCRIPTION:{0}", w.descriptionHtml));
            str.AppendLine("LOCATION: Online");
            str.AppendLine("BEGIN:VALARM");
            str.AppendLine("TRIGGER:-PT10M");
            str.AppendLine("ACTION:DISPLAY");
            str.AppendLine("DESCRIPTION:Reminder");
            str.AppendLine("END:VALARM");
            str.AppendLine("END:VEVENT");
            str.AppendLine("END:VCALENDAR");

            return w.mentors[0];
        }

        internal async Task SendEmail(StringBuilder content, StringBuilder icsFileContent, IEnumerable<Mentor> mentorsFromDB, string mentor)
        {
            var mentors = new Dictionary<string, string>();
            var apiKey = Environment.GetEnvironmentVariable("EMAILAPIKEY", EnvironmentVariableTarget.Process);
            var emailSender = Environment.GetEnvironmentVariable("EMAILSENDER", EnvironmentVariableTarget.Process);

            var client = new SendGridClient(apiKey);

            var icsAttachment = new Attachment()
            {
                Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(icsFileContent.ToString())),
                Type = "text/calendar",
                Filename = "meeting.ics",
                Disposition = "inline",
                ContentId = "Attachment"
            };

            var msg = new SendGridMessage();

            msg.SetFrom(new EmailAddress(emailSender, "CoderDojo"));
            msg.SetSubject("Dein CoderDojo Online Workshop");
            msg.AddAttachment(icsAttachment);

            var mentorFromDB = mentorsFromDB.FirstOrDefault(mdb => mdb.firstname == mentor);
            if (mentorFromDB == null)
            {
                return;
            }

            mentors.Add(mentorFromDB.nickname, mentorFromDB.email);
            msg.AddTo(new EmailAddress(mentors[mentorFromDB.firstname]));
            msg.AddContent(MimeType.Text, content.ToString());
            msg.AddContent(MimeType.Html, content.ToString());
            var response = await client.SendEmailAsync(msg);
        }
    }
}
