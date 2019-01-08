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
        OpusStream OpusStream;
        LavaStream Stream;

        public readonly ulong GuildId;
        public readonly ulong UserId;

        public LavaGuildConnection(ulong guildId, ulong userId)
        {
            GuildId = guildId;
            UserId = userId;
        }

        public async Task Play(string track, long startTime = 0, long endTime = -1)
        {
            if (track.StartsWith("yt:"))
            {
                Stream = await Youtube.GetStream(track.Substring(3));
            }
            else
            {
                Console.WriteLine("Unknown track: " + track);
            }
            CreateStream();
        }

        public async Task VoiceUpdate(string sessionId, string endpoint, string token)
        {
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
            try
            {
                await webSocket.ConnectAsync(new Uri($"wss://{endpoint}/?v=3"), CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
            audioConnection = new AudioConnection(webSocket);
            await audioConnection.Connect(GuildId, UserId, sessionId, token);
        }

        public async Task Message(JObject packet)
        {
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
        // {"playlistInfo":{"name":null,"selectedTrack":0},"loadType":3,"tracks":[{"track":null,"info":{"identifier":"OmP1iZl1gH8","isSeekable":true,"author":"phatrobshow","length":1,"isStream":false,"position":0,"title":"1 second long video","uri":"https://www.youtube.com/watch?v=OmP1iZl1gH8"}}]}
        // {"playlistInfo":{},"loadType":"TRACK_LOADED","tracks":[{"track":"QAAAeAIAEzEgc2Vjb25kIGxvbmcgdmlkZW8AC3BoYXRyb2JzaG93AAAAAAAAA+gAC09tUDFpWmwxZ0g4AAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9T21QMWlabDFnSDgAB3lvdXR1YmUAAAAAAAAAAA==","info":{"identifier":"OmP1iZl1gH8","isSeekable":true,"author":"phatrobshow","length":1000,"isStream":false,"position":0,"title":"1 second long video","uri":"https://www.youtube.com/watch?v=OmP1iZl1gH8"}}]}
        public void CreateStream()
        {
            if (!audioConnection.Ready)
                SpinWait.SpinUntil(() => audioConnection.Ready);
            byte[] buffer = new byte[OpusEncoder.FRAME_BYTES];
            int read;
            OpusStream = new OpusStream(audioConnection.SendOpusAsync, ()=>
            {
                try
                {
                    if ((read = Stream.Decoder.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        Console.WriteLine("len: " + Stream.OldDecoder.WaveFormat.BytesToMilliseconds(Stream.OldDecoder.Position)/1000f);
                        OpusStream.Write(buffer, 0, read);
                    }
                    else
                    {
                        OpusStream.StopStream();
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                return Task.CompletedTask;
            }, audioConnection.SetSpeakingAsync);
            OpusStream.StartStream();
        }
    }
}
