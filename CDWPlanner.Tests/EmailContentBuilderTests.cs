using CDWPlanner.DTO;
using Moq;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CDWPlanner.Tests
{
    public class EmailContentBuilderTests
    {
        [Fact]
        public void BuildEmailTest()
        {
            var ws = new Workshop
            {
                begintime = new DateTime(2020, 1, 1, 13, 0, 0).ToString("o"),
                endtime = new DateTime(2020, 1, 1, 14, 0, 0).ToString("o"),
                description = "*Bar*",
                title = "Foo",
                targetAudience = "FooBar",
                mentors = new List<string> {"Tester"},
                zoomUser = "fooemail",
                zoom = "foo"

            };
            var emailBuilder = new EmailContentBuilder();
            var result = emailBuilder.BuildEmailAndICSFile(ws, "dummyhostkey");

            var debugString = @$"Hallo {ws.mentors[0]}!<br><br>Danke, dass du einen Workshop beim Online CoderDojo anbietest. In diesem Email erhältst du alle Zugangsdaten:<br><br>Titel: {ws.titleHtml}<br>Startzeit: {ws.begintimeAsShortTime}<br>Endzeit: {ws.endtimeAsShortTime}<br>Beschreibung: {ws.descriptionHtml}<br>Zoom User: {ws.zoomUser}<br>Zoom URL: {ws.zoom}<br>Dein Hostkey: dummyhostkey<br><br>Viele Grüße,<br>Dein CoderDojo Organisationsteam";

            Debug.WriteLine(result.EmailContent);
            Assert.Contains(
                debugString,
                result.EmailContent);
        }
    }
}
