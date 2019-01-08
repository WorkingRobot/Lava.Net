using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class AudioConnection
    {
        ClientWebSocket Socket;

        int heartbeatInterval;
        uint ssrc;
        byte[] secretKey;
        public string ip;
        public ushort port;
        UdpClient UdpClient;

        public bool Ready { private set; get; }

        CancellationTokenSource heartbeatTokenSource;
        Task heartbeatTask;

        Task recieveTask;

        public AudioConnection(ClientWebSocket socket)
        {
            Console.WriteLine("Audio Connection created");
            Socket = socket;
        }

        public Task Connect(ulong guild, ulong user, string sessionId, string token)
        {
            recieveTask = RecieveTask();
            Console.WriteLine($"Sending IDENTIFY");
            var obj = JsonConvert.SerializeObject(
                new VoicePayload(0,
                new IdentifyPayload(guild, user, sessionId, token))
                );
            return Socket.SendAsync(Encoding.UTF8.GetBytes(obj), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        Task Select()
        {
            Console.WriteLine($"Sending SELECT");
            return Socket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                new VoicePayload(1,
                new SelectPayload(ip, port))
                )), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public Task SetSpeakingAsync(bool speaking)
        {
            if (!Ready)
            {
                SpinWait.SpinUntil(() => Ready);
            }
            try
            {
                Console.WriteLine($"Sending SPEAKING");
                Socket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                    new VoicePayload(5,
                    new SpeakingPayload(speaking, ssrc))
                    )), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return Task.CompletedTask;
        }

        async Task Recieve(JToken data)
        {
            switch (data["op"].ToObject<int>())
            {
                case 2: // Voice Ready
                    Console.WriteLine($"Recieved READY");
                    ssrc = data["d"]["ssrc"].ToObject<uint>();
                    ip = data["d"]["ip"].ToString();
                    port = data["d"]["port"].ToObject<ushort>();
                    await Select();
                    return;
                case 8: // Hello o/
                    Console.WriteLine($"Recieved HELLO");
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
                case 4: // Session Description
                    Console.WriteLine($"Recieved SESSION DESC");
                    if (data["d"]["mode"].ToString() != "xsalsa20_poly1305")
                    {
                        Console.WriteLine("Unknown mode: " + data["d"]["mode"].ToString());
                        return;
                    }
                    secretKey = data["d"]["secret_key"].ToObject<byte[]>();
                    try
                    {
                        UdpClient = new UdpClient();
                        UdpClient.Connect(ip, port);
                        await SendUdpDiscoveryAsync(ssrc);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                    Ready = true;
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
                            var _ = Recieve(JObject.Parse(Encoding.UTF8.GetString(buffer)));
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
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(heartbeatInterval, token);
                    await Socket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                       new VoicePayload(3, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                       )), WebSocketMessageType.Text, true, token);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Heartbeat Exception: {0}", e);
            }
        }

        Task SendUdpDiscoveryAsync(uint ssrc)
        {
            var packet = new byte[70];
            packet[0] = (byte)(ssrc >> 24);
            packet[1] = (byte)(ssrc >> 16);
            packet[2] = (byte)(ssrc >> 8);
            packet[3] = (byte)(ssrc >> 0);
            return SendUdpAsync(packet);
        }

        ulong udpKeepAlive;
        Task SendUdpKeepaliveAsync()
        {
            var value = udpKeepAlive++;
            var packet = new byte[8];
            packet[0] = (byte)(value >> 0);
            packet[1] = (byte)(value >> 8);
            packet[2] = (byte)(value >> 16);
            packet[3] = (byte)(value >> 24);
            packet[4] = (byte)(value >> 32);
            packet[5] = (byte)(value >> 40);
            packet[6] = (byte)(value >> 48);
            packet[7] = (byte)(value >> 56);
            return SendUdpAsync(packet);
        }

        Task SendUdpAsync(byte[] buffer) => UdpClient.SendAsync(buffer, buffer.Length);
        
        public Task SendOpusAsync(byte[] buffer, int offset, int length, uint timestamp, ushort sequence)
        {
            byte[] opus = new byte[length];
            Buffer.BlockCopy(buffer, offset, opus, 0, length);
            return SendOpusAsync(opus, timestamp, sequence);
        }

        public Task SendOpusAsync(byte[] opus, uint timestamp, ushort sequence)
        {
            try
            {
                if (!Ready)
                {
                    SpinWait.SpinUntil(() => Ready);
                }
                var nonce = RtpEncode(sequence, timestamp, ssrc);
                var sodium = SodiumEncode(opus, nonce, secretKey);

                byte[] buffer = new byte[12 + sodium.Length];
                Buffer.BlockCopy(nonce, 0, buffer, 0, 12);
                Buffer.BlockCopy(sodium, 0, buffer, 12, sodium.Length);
                return SendUdpAsync(buffer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Send Opus Exception: {0}", e);
                return Task.CompletedTask;
            }
        }

        byte[] RtpEncode(ushort sequence, uint timestamp, uint ssrc)
        {
            byte[] header = new byte[24];

            header[0] = 0x80;
            header[1] = 0x78;

            var flip = BitConverter.IsLittleEndian;
            var seqnb = BitConverter.GetBytes(sequence);
            var tmspb = BitConverter.GetBytes(timestamp);
            var ssrcb = BitConverter.GetBytes(ssrc);

            if (flip)
            {
                Array.Reverse(seqnb);
                Array.Reverse(tmspb);
                Array.Reverse(ssrcb);
            }

            Array.Copy(seqnb, 0, header, 2, seqnb.Length);
            Array.Copy(tmspb, 0, header, 4, tmspb.Length);
            Array.Copy(ssrcb, 0, header, 8, ssrcb.Length);

            return header;
        }

        [DllImport("libsodium", CallingConvention = CallingConvention.Cdecl, EntryPoint = "crypto_secretbox_easy")]
        static extern int CreateSecretBox(byte[] buffer, byte[] message, long messageLength, byte[] nonce, byte[] key);

        byte[] SodiumEncode(byte[] input, byte[] nonce, byte[] secretKey)
        {
            if (secretKey == null || secretKey.Length != 32)
                throw new ArgumentException("Invalid key.");

            if (nonce == null || nonce.Length != 24)
                throw new ArgumentException("Invalid nonce.");

            var buff = new byte[16 + input.Length];
            var err = CreateSecretBox(buff, input, input.Length, nonce, secretKey);

            if (err != 0)
                throw new CryptographicException("Error encrypting data.");

            return buff;
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

        struct SelectPayload
        {
            public string protocol;
            public Data data;

            public struct Data
            {
                public string address;
                public ushort port;
                public string mode;
            }

            public SelectPayload(string ip, ushort port)
            {
                protocol = "udp";
                data = new Data()
                {
                    address = ip,
                    port = port,
                    mode = "xsalsa20_poly1305"
                };
            }
        }

        struct SpeakingPayload
        {
            public bool speaking;
            public uint delay;
            public uint ssrc;

            public SpeakingPayload(bool speaking, uint ssrc)
            {
                this.speaking = speaking;
                delay = 0;
                this.ssrc = ssrc;
            }
        }
    }
}
