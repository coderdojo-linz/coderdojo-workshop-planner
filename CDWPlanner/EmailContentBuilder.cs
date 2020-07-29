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
    public class EmailContentBuilder
    {
        internal string BuildEmailAndICSFile(StringBuilder emailContent, StringBuilder str, Workshop w, string host_key)
        {
            if (w.mentors.Count == 0)
            {
                return null;
            }


            emailContent.Append($"Hallo {w.mentors[0]}!<br><br>");
            emailContent.Append($"Danke, dass du einen Workshop beim Online CoderDojo anbietest. In diesem Email erhältst du alle Zugangsdaten:<br><br>");
            emailContent.Append($"Titel: {w.titleHtml}<br>Startzeit: {w.begintimeAsShortTime}<br>Endzeit: {w.endtimeAsShortTime}<br>Beschreibung: {w.descriptionHtml}");
            emailContent.Append($"<br>Zoom User: {w.zoomUser}<br>Zoom URL: {w.zoom}<br>Dein Hostkey: {host_key}<br><br>");
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
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString));
            str.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU");
            str.AppendLine("END:DAYLIGHT");
            str.AppendLine("BEGIN:STANDARD");
            str.AppendLine("TZOFFSETFROM:+0200");
            str.AppendLine("TZOFFSETTO:+0100");
            str.AppendLine("TZNAME:CET");
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString));
            str.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
            str.AppendLine("END:STANDARD");
            str.AppendLine("END:VTIMEZONE");
            str.AppendLine("BEGIN:VEVENT");
            str.AppendLine("X-WR-RELCALID:XXXXXX");
            str.AppendLine("X-MS-OLK-FORCEINSPECTOROPEN:TRUE");
            str.AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmssZ}", w.begintimeAsIcsString));
            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmss}", w.begintimeAsIcsString));
            str.AppendLine(string.Format("DTEND:{0:yyyyMMddTHHmmss}", w.endtimeAsIcsString));
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

        internal async Task BuildAndSendEmail(StringBuilder content, StringBuilder icsFileContent, IEnumerable<Mentor> mentorsFromDB, string mentor)
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
