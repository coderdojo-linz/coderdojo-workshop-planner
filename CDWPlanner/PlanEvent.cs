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

namespace CDWPlanner
{
    public class PlanEvent
    {
        private readonly IGitHubFileReader fileReader;
        private readonly IDataAccess dataAccess;

        public PlanEvent(IGitHubFileReader fileReader, IDataAccess dataAccess)
        {
            this.fileReader = fileReader;
            this.dataAccess = dataAccess;
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
                    log.LogInformation("Wrong YML Format, check README.md for right format");
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

            var parsedDateEvent = DateTime.SpecifyKind(DateTime.Parse(dateFolder), DateTimeKind.Utc);
            var dbEventsFound = await dataAccess.ReadWorkshopForDateAsync(parsedDateEvent);
            var found = dbEventsFound != null;

            // Get workshops and write it into an array only if draft flag is false
            var workshopData = new BsonArray();
            foreach (var w in workshopOperation.Workshops.workshops.Where(ws => !ws.draft))
            {
                workshopData.Add(w);
            }

            var eventData = BuildEventDocument(parsedDateEvent, workshopData);

            // Check wheather a new file exists, create/or modifie it
            if (operation == "added" || found == false)
            {
                await dataAccess.InsertIntoDBAsync(eventData);
                found = true;
            }

            if (operation == "modified" || found == true)
            {
                await dataAccess.ReplaceDataOfDBAsync(parsedDateEvent, eventData);
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

            if(workshopData == null || workshopData.Count == 0)
            {
                eventData["location"] += " - Themen werden noch bekannt gegeben";
            }
            return eventData;
        }

        // Get the workshop body array
        [FunctionName("GetDBContent")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var date = req.Query["date"];

            var parsedDateEvent = DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc);
            var dbEventsFound = await dataAccess.ReadWorkshopForDateAsync(parsedDateEvent);

            var workshops = dbEventsFound.GetElement("workshops");
            var responseBuilder = new StringBuilder(@"<section class='main'><table width = '100%'>
                                    <tbody><tr><td>&nbsp;</td><td class='main-td' width='600'>
			                        <h1>Hallo&nbsp;*|FNAME|*,</h1>
			                        <p>Diesen Freitag ist wieder CoderDojo-Nachmittag und es sind viele Workshops im Angebot.Hier eine kurze <strong>Orientierungshilfe</strong>:</p>
                                    ");
            foreach (var w in workshops.Value.AsBsonArray)
            {
                AddWorkshopHtml(responseBuilder, w);
            }

            responseBuilder.Append(@"</td></tr></tbody></table></section>");

            var responseMessage = responseBuilder.ToString();

            return new OkObjectResult(responseMessage);
        }

        // Build the html string
        internal static void AddWorkshopHtml(StringBuilder responseBuilder, BsonValue w)
        {
            static string ExtractTime(string begintime) => begintime.Replace(":00Z", string.Empty).Split("T").Last();

            var begintime = w["begintime"].ToString();
            var endtime = w["endtime"].ToString();
            var description = Markdown.ToHtml(w["description"].ToString())[3..^5];
            var title = Markdown.ToHtml(w["title"].ToString())[3..^5];
            var targetAudience = Markdown.ToHtml(w["targetAudience"].ToString())[3..^5];
            var bTime = ExtractTime(begintime);
            var eTime = ExtractTime(endtime);
            var timeString = $"{bTime} - {eTime}";

            responseBuilder.Append($@"<h3>{title}</h3><p class='subtitle'>{timeString}<br/>{targetAudience}</p><p>{description}</p>");
        }
    }
}
