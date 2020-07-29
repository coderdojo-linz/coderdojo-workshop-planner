using CDWPlanner.DTO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace CDWPlanner.Tests
{
    public class NewsletterHtmlBuilderTests
    {
        [Fact]
        public void TestHtmlGeneration()
        {
            var ws = new Workshop
            {
                begintime = new DateTime(2020, 1, 1, 13, 0, 0).ToString("o"),
                endtime = new DateTime(2020, 1, 1, 14, 0, 0).ToString("o"),
                description = "*Bar*",
                title = "Foo",
                targetAudience = "FooBar"
            };
            var builder = new StringBuilder();
            var htmlBuilder = new NewsletterHtmlBuilder();
            htmlBuilder.AddWorkshopHtml(builder, ws);

            var debugString = "<h3>Foo</h3><p class='subtitle'>13:00 - 14:00<br/>FooBar</p><p><em>Bar</em></p>";

            Debug.WriteLine(builder.ToString());
            Assert.Contains(
                debugString,
                builder.ToString());
        }
    }
}
