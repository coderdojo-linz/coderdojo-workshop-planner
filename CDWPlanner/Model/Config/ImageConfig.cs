using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using YamlDotNet.Serialization;


namespace CDWPlanner.Model.Config
{
    public partial class ImageConfig
    {
        [JsonProperty("keywords")]
        [YamlMember(Alias = "keywords")]
        public KeywordConfig[] Keywords { get; set; }

        [JsonProperty("shortCodes")]
        [YamlMember(Alias = "shortCodes")]
        public ShortCodeConfig[] ShortCodes { get; set; }
    }

    public partial class KeywordConfig
    {
        [YamlMember(Alias = "keyword")]
        public string Keyword { get; set; }

        [YamlMember(Alias = "Thumbnail")]
        public string Thumbnail { get; set; }

        [YamlMember(Alias = "alias")]
        public string[] Alias { get; set; }

        public bool Contains(string text, bool includeAlias = false)
        {
            if (text.Contains(Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            if (includeAlias && Alias != null)
            {
                return Alias.Any(a => text.Contains(a, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
    }

    public partial class ShortCodeConfig
    {
        [YamlMember(Alias = "shortCode")]
        public string Code { get; set; }

        [YamlMember(Alias = "alias")]
        public string[] Alias { get; set; }

        [YamlMember(Alias = "Thumbnail")]
        public string Thumbnail { get; set; }

        // Checks if shortcode and the code matches, include alias if required
        public bool Equals(string shortCode, bool includeAlias = false)
        {
            if (Code.Equals(shortCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (includeAlias && Alias != null)
            {
                return Alias.Any(a => a.Equals(shortCode, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
    }
}
