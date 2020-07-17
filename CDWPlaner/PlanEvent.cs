using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using Microsoft.Azure.WebJobs.ServiceBus;
using CDWPlaner.DTO;
using Markdig;

namespace CDWPlaner
{
    public class PlanEvent
    {
        private readonly HttpClient client;
        private readonly IGitHubFileReader fileReader;

        public PlanEvent(IHttpClientFactory clientFactory, IGitHubFileReader fileReader)
        {
            client = clientFactory.CreateClient();
            this.fileReader = fileReader;
        }

        [FunctionName("PlanEvent")]
        public async Task<IActionResult> ReceiveFromGitHub(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [ServiceBus("workshopupdate", Connection = "ServiceBusConnection", EntityType = EntityType.Topic)] ICollector<WorkshopOperation> collector,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var gitHubDataObject = JsonSerializer.Deserialize<GitHubData>(requestBody);
            var commitId = gitHubDataObject.commits.Select(c => c.id);

            // Get folder and file info from latest commit
            static IEnumerable<FolderFileInfo> GetFolderAndFile(IEnumerable<string> items) =>
                items.Distinct()
                    .Select(item => new { fullFolder = item, splittedFolder = item.Split("/") })
                    .Where(item => item.splittedFolder.Length >= 2)
                    .Where(item => item.splittedFolder[^1].EndsWith(".yml") || item.splittedFolder[^1].EndsWith(".yaml"))
                    .Where(item => Regex.IsMatch(item.splittedFolder[^2], @"^\d{4}-\d{2}-\d{2}$"))
                    .Select(item => new FolderFileInfo { FullFolder = item.fullFolder, DateFolder = item.splittedFolder[^2], File = item.splittedFolder[^1] });

            // Add them to a list, one for modiefied, one for new folders/files
            var commitListAdded = GetFolderAndFile(gitHubDataObject.commits.SelectMany(c => c.added))
                .Select(c => new WorkshopOperation { Operation = "added", FolderInfo = c });
            var commitListChanged = GetFolderAndFile(gitHubDataObject.commits.SelectMany(c => c.modified))
                .Select(c => new WorkshopOperation { Operation = "modified", FolderInfo = c });

            try
            {
                // Add the data to the collection
                foreach (var item in commitListAdded.Concat(commitListChanged))
                {
                    item.Workshops = await fileReader.GetYMLFileFromGitHub(item.FolderInfo, commitId);
                    collector.Add(item);
                }
            }
            catch (Exception)
            {
                log.LogInformation("Wrong YML Format, check README.md for right format");
                return new BadRequestResult();
            }

            return new AcceptedResult();
        }

        // Subscribtion of PlanEvent Topic
        // Writes data to MongoDB
        [FunctionName("WriteEventToDB")]
        [Obsolete]
        public async Task Receive(
            [ServiceBusTrigger("workshopupdate", "transfer-to-db", Connection = "ServiceBusConnection")] string workshopJson,
            ILogger log)
        {
            // Now it's JSON
            var workshopOperation = JsonSerializer.Deserialize<WorkshopOperation>(workshopJson);

            var begintime = DateTime.Now;
            var endtime = DateTime.Now;
            var description = string.Empty;
            var prerequisites = string.Empty;
            var mentors = new List<string> { };
            var title = string.Empty;
            var targetAudience = string.Empty;
            var zoom = string.Empty;

            var dateFolder = workshopOperation?.FolderInfo?.DateFolder;
            // modified or added
            var operation = workshopOperation?.Operation;

            var dbCollection = GetCollectionFromServer();
            var parsedDateEvent = DateTime.SpecifyKind(DateTime.Parse(dateFolder), DateTimeKind.Utc); ;

            // To filter all existing documents with specific foldername
            var dateFilter = new BsonDocument("date", parsedDateEvent);
            var dbEventsFound = dbCollection.Find(dateFilter).ToList();

            var workshopData = new BsonArray();
            var found = true;

            // If document with the same date already exists, check if's after foreach
            if (dbEventsFound.Count == 0)
            {
                found = false;
            }

            // Get workshops and write it into an array only if draft flag is false
            foreach (var w in workshopOperation.Workshops.workshops.Where(ws => !ws.draft))
            {
                // No workshops found
                if (w == null)
                {
                    break;
                }

                // Get workshop data
                begintime = w.begintime;
                endtime = w.endtime;
                description = w.description;
                prerequisites = w.prerequisites;
                mentors = w.mentors;
                title = w.title;
                targetAudience = w.targetAudience;
                zoom = w.zoom;

                // It's YML/ Convert it into one
                workshopData.Add(new BsonDocument {
                        { "begintime" , begintime},
                        { "endtime" , endtime},
                        { "title" , title},
                        { "targetAudience" , targetAudience},
                        { "description" , description},
                        { "prerequisites" , prerequisites},
                        { "mentors", new BsonArray(mentors)},
                        { "zoom" , zoom }
                    });
            }

            var eventData = new BsonDocument {
                    dateFilter,
                    { "type", "CoderDojo Virtual" },
                    { "location", "CoderDojo Online - Themen werden noch bekanntgegeben" },
                    { "workshops", workshopData}
                };

            // Check wheather a new file exists, create/or modifie it
            if (operation == "added" || found == false)
            {
                dbCollection.InsertOne(eventData);
                found = true;
            }

            if (operation == "modified" || found == true)
            {
                dbCollection.ReplaceOne(dateFilter, eventData);
            }

            // Just some receive message
            log.LogInformation("RECEIVED");
            await Task.Delay(0);
        }

        [FunctionName("GetDBContent")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var date = req.Query["date"];

            var dbCollection = GetCollectionFromServer();
            var parsedDateEvent = DateTime.SpecifyKind(DateTime.Parse(date), DateTimeKind.Utc); ;

            // To filter all existing documents with specific foldername
            var dateFilter = new BsonDocument("date", parsedDateEvent);
            var dbEventsFound = dbCollection.Find(dateFilter).ToList();

            var workshops = dbEventsFound[0].GetValue("workshops");

            var begintime = string.Empty;
            var endtime = string.Empty;
            var description = string.Empty;
            var title = string.Empty;
            var targetAudience = string.Empty;
            string[] responseArray = new string[10];
            string[] bTime = new string[10];
            string[] eTime = new string[10];
            var timeString = string.Empty;
            var responseString = string.Empty;
            var responseBody = string.Empty;
            var responseBegin = @"<section class='main'><table width = '100%'>
                                    <tbody><tr><td>&nbsp;</td><td class='main-td' width='600'>
			                        <h1>Hallo&nbsp;*|FNAME|*,</h1>
			                        <p>Diesen Freitag ist wieder CoderDojo-Nachmittag und es sind viele Workshops im Angebot.Hier eine kurze <strong>Orientierungshilfe</strong>:</p>
                                    ";
            var responseEnd = @"</td></tr></tbody></table></section>";
            for (int i = 0; i < workshops.AsBsonArray.Count; i++)
            {
                foreach (var w in workshops.AsBsonArray)
                {
                    begintime = w["begintime"].ToString();
                    endtime = w["endtime"].ToString();
                    description = w["description"].ToString();
                    title = w["title"].ToString();
                    targetAudience = w["targetAudience"].ToString();

                    bTime = begintime.Replace(":00Z", string.Empty).Split("T");
                    eTime = endtime.Replace(":00Z", string.Empty).Split("T");
                    timeString = bTime[1] + " - " + eTime[1];

                    responseArray[i] = $@"<h3>{title}</h3>
                                          <p class=subtitle'>{timeString}<br/>
                                          {targetAudience}</p>
                                          <p>{description}</p>";
                    i++;
                }
            }
            for (int i = 0; i < responseArray.Length; i++)
            {
                responseBody += responseArray[i];
            }

            responseString = responseBegin + responseBody + responseEnd;

            string responseMessage = Markdown.ToHtml(responseString);

            return new OkObjectResult(responseMessage);
        }


        //Connect with server and get collection
        public IMongoCollection<BsonDocument> GetCollectionFromServer()
        {
            var dbuser = Environment.GetEnvironmentVariable("MONGOUSER", EnvironmentVariableTarget.Process);
            var dbpassword = Environment.GetEnvironmentVariable("MONGOPASSWORD", EnvironmentVariableTarget.Process);

            var urlMongo = $"mongodb://{dbuser}:{dbpassword}@ds042898.mlab.com:42898/member-management-test/?retryWrites=false";
            var dbClient = new MongoClient(urlMongo);

            var dbServer = dbClient.GetDatabase("member-management-test");
            var dbCollection = dbServer.GetCollection<BsonDocument>("events");

            return dbCollection;
        }
    }
}
