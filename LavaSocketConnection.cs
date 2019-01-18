using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class LavaSocketConnection
    {
        public readonly WebSocket Socket;
        public readonly int ShardCount;
        public readonly ulong UserId;
        public SortedDictionary<ulong, LavaGuildConnection> Connections = new SortedDictionary<ulong, LavaGuildConnection>();

        public LavaSocketConnection(WebSocket socket, int shardCount, ulong userId)
        {
            Socket = socket;
            ShardCount = shardCount;
            UserId = userId;
        }

        public async Task HandleAsync()
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
                            ulong guildId = obj["guildId"].ToObject<ulong>();
                            var _ = GetConnection(guildId).Message(obj);
                            break;
                    }

                    Array.Clear(buffer, 0, 1024);
                }
            }
            catch (WebSocketException)
            {
                Console.WriteLine("Connection closed by client");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
            }
            finally
            {
                if (Socket != null)
                    Socket.Dispose();
                await Task.WhenAll(Connections.Values.Select(v => v.Disconnect())).ConfigureAwait(false);
            }
        }

        public LavaGuildConnection GetConnection(ulong guildId)
        {
            if (Connections.TryGetValue(guildId, out LavaGuildConnection ret))
            {
                return ret;
            }
            return Connections[guildId] = new LavaGuildConnection(guildId, UserId, this);
        }

        public async Task<bool> RemoveConnection(ulong guildId)
        {
            if (Connections.TryGetValue(guildId, out LavaGuildConnection ret))
            {
                await ret.Disconnect();
                Connections.Remove(guildId);
                return true;
            }
            return false;
        }
    }
}
