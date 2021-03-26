using CDWPlanner.DTO;
using System.Collections.Generic;
using System.Text;

namespace CDWPlanner
{
    public class NewsletterHtmlBuilder
    {
        public string BuildNewsletterHtml(IEnumerable<Workshop> workshops)
        {
            var responseBuilder = new StringBuilder(@"<section class='main'><table width = '100%'>
                                    <tbody><tr><td>&nbsp;</td><td class='main-td' width='600'>
			                        <h1>Hallo&nbsp;*|FNAME|*,</h1>
			                        <p>Diesen Freitag ist wieder CoderDojo-Nachmittag und es sind viele Workshops im Angebot. Hier eine kurze <strong>Orientierungshilfe</strong>:</p>
                                    ");
            foreach (var w in workshops)
            {
                AddWorkshopHtml(responseBuilder, w);
            }

            responseBuilder.Append(@"</td><td>&nbsp;</td></tr></tbody></table></section>");
            return responseBuilder.ToString();
        }

        // Build the html string
        internal void AddWorkshopHtml(StringBuilder responseBuilder, Workshop w)
        {
            var timeString = $"{w.begintimeAsShortTime} - {w.endtimeAsShortTime}";

            responseBuilder.Append($@"<h3>{w.titleHtml}</h3><p class='subtitle'>{timeString}<br/>{w.targetAudienceHtml}</p><p>{w.descriptionHtml}</p>");
        }
    }
}
