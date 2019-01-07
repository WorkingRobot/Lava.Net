using Lava.Net.Sources.Youtube;
using Lava.Net.Streams;
using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class LavaGuildConnection
    {
        ClientWebSocket webSocket;
        AudioConnection audioConnection;
        LavaStream Stream;
        Task StreamingTask;

        public readonly ulong GuildId;
        public readonly ulong UserId;

        public LavaGuildConnection(ulong guildId, ulong userId)
        {
            GuildId = guildId;
            UserId = userId;
        }

        public async Task Play(string track, long startTime = 0, long endTime = -1)
        {
            Console.WriteLine("Recieved Play");
            if (track.StartsWith("yt:"))
            {
                Stream = await Youtube.GetStream(track.Substring(3));
            }
            await audioConnection.SetSpeakingAsync(true);
            Console.WriteLine("Speaking set");
            StreamingTask = CreateStreamingTask();
        }

        public async Task VoiceUpdate(string sessionId, string endpoint, string token)
        {
            Console.WriteLine("Recieved Voice Update");
            if (webSocket != null)
            {
                if (webSocket.State != WebSocketState.None && webSocket.State != WebSocketState.Closed)
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }

            if (endpoint.EndsWith(":80"))
            {
                endpoint = endpoint.Substring(0, endpoint.Length - 3);
            }

            webSocket = new ClientWebSocket();
            //webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            Console.WriteLine("Connecting! "+webSocket.Options.KeepAliveInterval.TotalMilliseconds);
            try
            {
                Console.WriteLine("Connecting to " + $"wss://{endpoint}/?v=4");
                await webSocket.ConnectAsync(new Uri($"wss://{endpoint}/?v=4"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
            Console.WriteLine("Connected!");
            audioConnection = new AudioConnection(webSocket);
            Console.WriteLine("Voice Connecting!");
            await audioConnection.Connect(GuildId, UserId, sessionId, token);
            Console.WriteLine("Voice Connected!");
        }

        public async Task Message(JObject packet)
        {
            Console.WriteLine($"Guild {GuildId} update with: " + packet.ToString());
            switch (packet["op"].ToString())
            {
                case "voiceUpdate":
                    if (string.IsNullOrEmpty(packet["event"].Value<string>("endpoint")))
                        return;
                    Console.WriteLine(packet.ToString());
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

        public async Task CreateStreamingTask()
        {
            Console.WriteLine("Streaming started");
            byte[] buffer = new byte[OpusEncoder.FRAME_BYTES];
            OpusStream opusStream = new OpusStream(audioConnection.SendOpusAsync);
            Console.WriteLine("buffer size: "+buffer.Length);
            try
            {
                int read;
                while ((read = Stream.Decoder.Read(buffer, 0, buffer.Length)) > 0)
                {
                    Console.WriteLine(Stream.Decoder.WaveFormat.BytesToMilliseconds(Stream.Decoder.Position)/1000f);
                    await opusStream.WriteAsync(buffer, 0, OpusEncoder.FRAME_BYTES, CancellationToken.None);
                    await Task.Delay(5);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Heartbeat Exception: {0}", e);
            }
            Console.WriteLine("finished");
        }
    }
}
