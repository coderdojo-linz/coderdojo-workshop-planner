using System;
using System.Collections.Generic;
using System.Text;

namespace CDWPlanner.Model
{
    public class DiscordSettings
    {
        public string Token { get; set; }

        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }

    }
}
