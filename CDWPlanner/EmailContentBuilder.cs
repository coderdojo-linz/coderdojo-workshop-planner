using CDWPlanner.DTO;
using SendGrid;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail;
using System.Linq;

namespace CDWPlanner
{
    public class EmailBuildingResult
    {
        public string EmailContent { get; set; }

        public string CalendarItem { get; set; }

        public string MentorName { get; set; }
    }

    public class EmailContentBuilder
    {
        internal EmailBuildingResult BuildEmailAndICSFile(Workshop w, string host_key)
        {
            if (w.mentors.Count == 0)
            {
                return null;
            }

            var emailContent = new StringBuilder()
                .Append($"Hallo {w.mentors[0]}!<br><br>")
                .Append($"Danke, dass du einen Workshop beim Online CoderDojo anbietest. In diesem Email erhältst du alle Zugangsdaten:<br><br>")
                .Append($"Titel: {w.titleHtml}<br>Startzeit: {w.begintimeAsShortTime}<br>Endzeit: {w.endtimeAsShortTime}<br>Beschreibung: {w.descriptionHtml}")
                .Append($"<br>Zoom User: {w.zoomUser}<br>Zoom URL: {w.zoom}<br>Dein Hostkey: {host_key}<br><br>")
                .Append($"Viele Grüße,<br>Dein CoderDojo Organisationsteam");

            // Build ics file
            var icsBuilder = new StringBuilder()
                .AppendLine("BEGIN:VCALENDAR")
                .AppendLine("VERSION:2.0")
                .AppendLine("PRODID:-//ical.marudot.com//iCal Event Maker")
                .AppendLine("CALSCALE:GREGORIAN")
                .AppendLine("METHOPD:PUBLISH")
                .AppendLine("CLASS:PUBLIC")
                .AppendLine("BEGIN:VTIMEZONE")
                .AppendLine("TZID:Europe/Vienna")
                .AppendLine("TZURL:http://tzurl.org/zoneinfo-outlook/Europe/Berlin")
                .AppendLine("X-LIC-LOCATION:Europe/Vienna")
                .AppendLine("BEGIN:DAYLIGHT")
                .AppendLine("TZOFFSETFROM:+0100")
                .AppendLine("TZOFFSETTO:+0200")
                .AppendLine("TZNAME:CEST")
                .AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString))
                .AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU")
                .AppendLine("END:DAYLIGHT")
                .AppendLine("BEGIN:STANDARD")
                .AppendLine("TZOFFSETFROM:+0200")
                .AppendLine("TZOFFSETTO:+0100")
                .AppendLine("TZNAME:CET")
                .AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString))
                .AppendLine("RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU")
                .AppendLine("END:STANDARD")
                .AppendLine("END:VTIMEZONE")
                .AppendLine("BEGIN:VEVENT")
                .AppendLine("X-WR-RELCALID:XXXXXX")
                .AppendLine("X-MS-OLK-FORCEINSPECTOROPEN:TRUE")
                .AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmssZ}", w.begintimeAsIcsString))
                .AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString))
                .AppendLine(string.Format("DTEND:{0:yyyyMMddTHHmmss}", w.endtimeAsIcsString))
                .AppendLine(string.Format("SUMMARY:{0}", w.titleHtml))
                .AppendLine("UID:20200727T072232Z-1947992826@marudot.com")
                .AppendLine("TZID:Europe/Vienna")
                .AppendLine(string.Format("DESCRIPTION:{0}", w.descriptionHtml))
                .AppendLine("LOCATION: Online")
                .AppendLine("BEGIN:VALARM")
                .AppendLine("TRIGGER:-PT10M")
                .AppendLine("ACTION:DISPLAY")
                .AppendLine("DESCRIPTION:Reminder")
                .AppendLine("END:VALARM")
                .AppendLine("END:VEVENT")
                .AppendLine("END:VCALENDAR");

            return new EmailBuildingResult
            {
                EmailContent = emailContent.ToString(),
                CalendarItem = icsBuilder.ToString(),
                MentorName = w.mentors[0]
            };
        }

        internal async Task BuildAndSendEmail(EmailBuildingResult result, IEnumerable<Mentor> mentorsFromDB)
        {
            var mentors = new Dictionary<string, string>();
            var apiKey = Environment.GetEnvironmentVariable("EMAILAPIKEY", EnvironmentVariableTarget.Process);
            var emailSender = Environment.GetEnvironmentVariable("EMAILSENDER", EnvironmentVariableTarget.Process);

            var client = new SendGridClient(apiKey);

            var icsAttachment = new Attachment()
            {
                Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(result.CalendarItem.ToString())),
                Type = "text/calendar",
                Filename = "meeting.ics",
                Disposition = "inline",
                ContentId = "Attachment"
            };

            var msg = new SendGridMessage();

            msg.SetFrom(new EmailAddress(emailSender, "CoderDojo"));
            msg.SetSubject("Dein CoderDojo Online Workshop");
            msg.AddAttachment(icsAttachment);

            var mentorFromDB = mentorsFromDB.FirstOrDefault(mdb => mdb.firstname == result.MentorName);
            if (mentorFromDB == null)
            {
                return;
            }

            mentors.Add(mentorFromDB.nickname, mentorFromDB.email);
            msg.AddTo(new EmailAddress(mentors[mentorFromDB.firstname]));
            msg.AddContent(MimeType.Text, result.EmailContent);
            msg.AddContent(MimeType.Html, result.EmailContent);
            var response = await client.SendEmailAsync(msg);
        }
    }
}
