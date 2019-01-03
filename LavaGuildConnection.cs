using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class LavaGuildConnection
    {
        ClientWebSocket webSocket;

        public readonly ulong GuildId;

        public LavaGuildConnection(ulong guildId)
        {
            GuildId = guildId;
        }

        public async Task Play(string identifier, long startTime = 0, long endTime = -1)
        {

        }

        public async Task VoiceUpdate(string sessionId, string endpoint, string token)
        {
            if (webSocket.State != WebSocketState.None && webSocket.State != WebSocketState.Closed)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }

            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri($"wss://{endpoint}/?v=4"), CancellationToken.None);
        }

        public async Task Message(JObject packet)
        {
            Console.WriteLine($"Guild {GuildId} update with: " + packet.ToString());
            switch (packet["op"].ToString())
            {
                case "voiceUpdate":
                    if (string.IsNullOrEmpty(packet["event"].Value<string>("endpoint")))
                        return;
                    await VoiceUpdate(packet.Value<string>("sessionId"), packet["event"].Value<string>("endpoint"), packet["event"].Value<string>("token"));
                    break;
                case "play":
                    var startTime = packet.Value<long>("startTime"); // 0 if unknown
                    if (!packet.TryGetValue("endTime", out var endTime)) // -1 if unknown
                        endTime = -1;
                    await Play(packet["track"].ToString(), startTime, endTime.ToObject<long>());
                    return;
            }
        }
    }
}
