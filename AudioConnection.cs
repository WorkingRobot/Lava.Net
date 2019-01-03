using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class AudioConnection
    {
        ClientWebSocket Socket;

        int heartbeatInterval;
        int ssrc;
        public string ip;
        public ushort port;

        CancellationTokenSource heartbeatTokenSource;
        Task heartbeatTask;

        public AudioConnection(ClientWebSocket socket)
        {
            Socket = socket;
        }

        public async Task Connect(ulong guild, ulong user, string sessionId, string token)
        {
            await Socket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                new VoicePayload(0, 
                new IdentifyPayload(guild, user, sessionId, token))
                )), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        async Task Recieve(JToken data)
        {
            switch (data["op"].ToObject<int>())
            {
                case 2: // Voice Ready
                    ssrc = data["d"]["ssrc"].ToObject<int>();
                    ip = data["d"]["ip"].ToString();
                    port = data["d"]["port"].ToObject<ushort>();
                    return;
                case 8: // Hello o/
                    heartbeatInterval = (int)(data["d"]["heartbeat_interval"].ToObject<int>() * 0.75f); // https://i.snag.gy/KylSGF.jpg
                    if (heartbeatTask != null)
                    {
                        heartbeatTokenSource.Cancel();
                        heartbeatTask = null;
                    }
                    heartbeatTokenSource = new CancellationTokenSource();
                    heartbeatTask = HeartbeatTask(heartbeatTokenSource.Token);
                    return;
                case 6: // Heartbeat ACK
                    return;
                default:
                    Console.WriteLine("UNKNOWN AUDIO OP: " + data.ToString());
                    return;
            }
        }

        public async Task RecieveTask()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (Socket.State == WebSocketState.Open)
                {
                    var result = await Socket.ReceiveAsync(buffer, CancellationToken.None);

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Close:
                            await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, CancellationToken.None);
                            break;
                        case WebSocketMessageType.Binary:
                            Console.WriteLine("Recieved binary input: " + Encoding.UTF8.GetString(buffer));
                            break;
                        case WebSocketMessageType.Text:
                            JObject obj = JObject.Parse(Encoding.UTF8.GetString(buffer));
                            var _ = Recieve(obj);
                            break;
                    }

                    Array.Clear(buffer, 0, 1024);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e);
            }
            finally
            {
                if (Socket != null)
                    Socket.Dispose();
            }
        }

        async Task HeartbeatTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Socket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                   new VoicePayload(3, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                   )), WebSocketMessageType.Text, true, token);
                await Task.Delay(heartbeatInterval, token);
            }
        }

        struct VoicePayload
        {
            public int op;
            public object d;

            public VoicePayload(int op, object d)
            {
                this.op = op;
                this.d = d;
            }
        }

        struct IdentifyPayload
        {
            public string server_id;
            public string user_id;
            public string session_id;
            public string token;

            public IdentifyPayload(ulong guild, ulong user, string sessionId, string token)
            {
                server_id = guild.ToString();
                user_id = user.ToString();
                session_id = sessionId;
                this.token = token;
            }
        }
    }
}
