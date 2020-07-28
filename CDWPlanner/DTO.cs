using Markdig;
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

    public class Event
    {
        public ObjectId _id { get; set; }

        public DateTime date { get; set; }

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
        public string titleHtml => Markdown.ToHtml(title)[3..^5];
        public string targetAudience { get; set; }
        public string targetAudienceHtml => Markdown.ToHtml(targetAudience)[3..^5];
        public string description { get; set; }
        public string descriptionHtml => Markdown.ToHtml(description)[3..^5];
        public string prerequisites { get; set; }
        public List<string> mentors { get; set; }
        public string shortCode { get; set; }
        public string zoomUser { get; set; }
        public string zoom { get; set; }

        public BsonDocument ToBsonDocument(DateTime baseDate) =>
            new BsonDocument {
                { "begintime" ,baseDate.Add(TimeSpan.Parse(begintime)).ToString("o") },
                { "endtime" , baseDate.Add(TimeSpan.Parse(endtime)).ToString("o")},
                { "title" , title},
                { "targetAudience" , targetAudience},
                { "description" , description},
                { "prerequisites" , prerequisites},
                { "mentors", new BsonArray(mentors)},
                { "zoomUser" , zoomUser },
                { "zoom" , zoom }
            };
    }

    public class WorkshopsRoot
    {
        public List<Workshop> workshops { get; set; }
    }
    public class Settings
    {
        public bool host_video { get; set; }
        public bool participant_video { get; set; }
        public bool cn_meeting { get; set; }
        public bool in_meeting { get; set; }
        public bool join_before_host { get; set; }
        public bool mute_upon_entry { get; set; }
        public bool watermark { get; set; }
        public bool use_pmi { get; set; }
        public int approval_type { get; set; }
        public string audio { get; set; }
        public string auto_recording { get; set; }
        public bool enforce_login { get; set; }
        public string enforce_login_domains { get; set; }
        public string alternative_hosts { get; set; }
        public bool close_registration { get; set; }
        public bool registrants_confirmation_email { get; set; }
        public bool waiting_room { get; set; }
        public bool request_permission_to_unmute_participants { get; set; }
        public bool registrants_email_notification { get; set; }
        public bool meeting_authentication { get; set; }
    }

    public class Meeting
    {
        public string uuid { get; set; }
        public long id { get; set; }
        public string host_id { get; set; }
        public string topic { get; set; }
        public int type { get; set; }
        public string status { get; set; }
        public DateTime start_time { get; set; }
        public int duration { get; set; }
        public string timezone { get; set; }
        public string agenda { get; set; }
        public DateTime created_at { get; set; }
        public string start_url { get; set; }
        public string join_url { get; set; }
        public string password { get; set; }
        public string h323_password { get; set; }
        public string pstn_password { get; set; }
        public string encrypted_password { get; set; }

        public Settings settings { get; set; }
    }

    public class MeetingsRoot
    {
        public List<Meeting> meetings { get; set; }
        public int page_count { get; set; }
        public int page_number { get; set; }
        public int page_size { get; set; }
        public int total_records { get; set; }
        public string next_page_token { get; set; }
    }

    public class Mentor
    {
        public ObjectId id { get; set; }
        public string nickname { get; set; }
        public string email { get; set; }
        public string firstname { get; set; }
        public string lastname { get; set; }
    }

    public class User
    {
        public DateTime created_at { get; set; }
        public string dept { get; set; }
        public string email { get; set; }
        public string first_name { get; set; }
        public string id { get; set; }
        public string language { get; set; }
        public string last_client_version { get; set; }
        public DateTime last_login_time { get; set; }
        public string last_name { get; set; }
        public string pic_url { get; set; }
        public object pmi { get; set; }
        public string status { get; set; }
        public string timezone { get; set; }
        public int type { get; set; }
        public int verified { get; set; }
        public string phone_number { get; set; }
        public string account_id { get; set; }
        public List<object> group_ids { get; set; }
        public string host_key { get; set; }
        public List<object> im_group_ids { get; set; }
        public string jid { get; set; }
        public string job_title { get; set; }
        public string location { get; set; }
        public string personal_meeting_url { get; set; }
        public string phone_country { get; set; }
        public string role_name { get; set; }
        public bool use_pmi { get; set; }
    }

    public class UsersRoot
    {
        public int page_count { get; set; }
        public int page_number { get; set; }
        public int page_size { get; set; }
        public int total_records { get; set; }
        public List<User> users { get; set; }

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
