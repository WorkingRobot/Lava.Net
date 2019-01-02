using Newtonsoft.Json.Linq;
using System;

namespace Lava.Net
{
    class LavaGuildConnection
    {
        public readonly ulong GuildId;
        public LavaGuildConnection(ulong guildId)
        {
            GuildId = guildId;
        }

        public void Play(string identifier, long startTime = 0, long endTime = -1)
        {

        }

        public void Message(JObject packet)
        {
            Console.WriteLine($"Guild {GuildId} update with: " + packet.ToString());
            switch (packet["op"].ToString())
            {
                case "play":
                    if (!packet.TryGetValue("startTime", out var startTime))
                        startTime = 0;
                    if (!packet.TryGetValue("endTime", out var endTime))
                        endTime = -1;
                    Play(packet["track"].ToString(), startTime.ToObject<long>(), endTime.ToObject<long>());
                    return;
            }
        }
    }
}
