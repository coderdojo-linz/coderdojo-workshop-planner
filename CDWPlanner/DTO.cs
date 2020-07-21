using MongoDB.Bson;
using System;
using System.Collections.Generic;

namespace CDWPlanner.DTO
{
    public class CommitObject
    {
        public Commit commit { get; set; }
    }
    public class Commit
    {
        public string id { get; set; }
        public List<string> added { get; set; }
        public List<string> removed { get; set; }
        public List<string> modified { get; set; }
    }

    public class GitHubData
    {
        public List<Commit> commits { get; set; }
    }

    public class Collection
    {
        public Id id { get; set; }

        public Date dateOfFile { get; set; }

        public string type { get; set; }

        public string location { get; set; }
        public List<Workshop> workshops { get; set; }
    }

    public class Date
    {
        public DateTimeOffset dateOfFile { get; set; }
    }

    public class Id
    {
        public string oid { get; set; }
    }

    public class Workshop
    {
        public string begintime { get; set; }
        public string endtime { get; set; }
        public bool draft { get; set; }
        public string title { get; set; }
        public string targetAudience { get; set; }
        public string description { get; set; }
        public string prerequisites { get; set; }
        public List<string> mentors { get; set; }
        public string zoom { get; set; }

        public BsonDocument ToBsonDocument(DateTime baseDate) =>
            new BsonDocument {
                { "begintime" , DateTime.SpecifyKind(baseDate.Add(TimeSpan.Parse(begintime)), DateTimeKind.Utc) },
                { "endtime" , DateTime.SpecifyKind(baseDate.Add(TimeSpan.Parse(endtime)), DateTimeKind.Utc) },
                { "title" , title},
                { "targetAudience" , targetAudience},
                { "description" , description},
                { "prerequisites" , prerequisites},
                { "mentors", new BsonArray(mentors)},
                { "zoom" , zoom }
            };
    }
    public class WorkshopsRoot
    {
        public List<Workshop> workshops { get; set; }
    }

    public class FolderFileInfo
    {
        public string FullFolder { get; set; }
        public string DateFolder { get; set; }
        public string File { get; set; }
    }

    public class WorkshopOperation
    {
        // added, modified
        public string Operation { get; set; }
        public FolderFileInfo FolderInfo { get; set; }
        public WorkshopsRoot Workshops { get; set; }
    }
}
