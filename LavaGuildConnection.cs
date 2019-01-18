using CSCore;
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
        public readonly LavaSocketConnection Source;

        public LavaGuildConnection(ulong guildId, ulong userId, LavaSocketConnection source)
        {
            GuildId = guildId;
            UserId = userId;
            Source = source;
        }

        public async Task Play(string track, long startTime = 0, long endTime = -1, bool noReplace = false)
        {
            if (!Stream?.Completed ?? false) // Stream exists but not complete
            {
                if (noReplace)
                {
                    return;
                }
                //OpusStream.StopStream();
                Stream.Close();
            }
            try
            {
                if (LavaConfig.Sources.Youtube && track.StartsWith("yt:"))
                {
                    Stream = await LavaSocketServer.Sources["youtube"].GetStream(track.Substring(3));
                    if (Stream.Decoder.CanSeek)
                    {
                        Stream.Decoder.SetPosition(TimeSpan.FromMilliseconds(startTime));
                    }
                }
                else if (LavaConfig.Sources.Soundcloud && track.StartsWith("sc:"))
                {
                    Stream = await LavaSocketServer.Sources["soundcloud"].GetStream(track.Substring(3));
                    if (Stream.Decoder.CanSeek)
                    {
                        Stream.Decoder.SetPosition(TimeSpan.FromMilliseconds(startTime));
                    }
                }
                else
                {
                    Console.WriteLine("Unknown track: " + track);
                }
                Stream.SetEndTime(endTime);
                CreateStream();
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
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
                Console.WriteLine(e);
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
                    await Play(packet["track"].ToString(), startTime, endTime.ToObject<long>(), packet.Value<bool>("noReplace"));
                    return;
                case "pause":
                    if (packet.TryGetValue("pause", out var paused))
                    {
                        Console.WriteLine(packet.ToString());
                        OpusStream.Pause(paused.Value<bool>());
                    }
                    return;
                case "stop": // Unsure if stop or destroy
                    OpusStream.StopStream();
                    Stream.Close();
                    return;
                case "destroy":
                    await Source.RemoveConnection(GuildId);
                    return;
            }
        }

        public void CreateStream()
        {
            if (OpusStream != null) return;
            if (!audioConnection.Ready)
                SpinWait.SpinUntil(() => audioConnection.Ready);
            byte[] buffer = new byte[OpusEncoder.FRAME_BYTES];
            int read;
            OpusStream = new OpusStream(audioConnection.SendOpusAsync, () =>
            {/*
                if (Stream.Completed)
                    return Task.CompletedTask;*/
                try
                {
                    if ((read = Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        //Console.WriteLine("len: " + Stream.OldDecoder.WaveFormat.BytesToMilliseconds(Stream.OldDecoder.Position)/1000f);
                        OpusStream.Write(buffer, 0, read);
                    }
                    else
                    {
                        //OpusStream.StopStream();
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                }
                return Task.CompletedTask;
            }, audioConnection.SetSpeakingAsync);
            OpusStream.StartStream();
        }

        public Task Disconnect()
        {
            OpusStream.StopStream();
            Stream.Close();
            return Task.WhenAll(audioConnection.Disconnect(), webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed connection", CancellationToken.None));
        }
    }
}
