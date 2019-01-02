using Lava.Net.Sources;
using Newtonsoft.Json;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class LavaSocketServer
    {
        private readonly string listenerUri;

        int count;

        public LavaSocketServer(string listenerUri)
        {
            this.listenerUri = listenerUri;
        }

        public async Task StartAsync()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(listenerUri);
            listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                var _ = ProcessRequest(context).ConfigureAwait(false);
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            if (context.Request.Headers.Get("Host") != LavaConfig.SERVER_URI || context.Request.Headers.Get("Authorization") != LavaConfig.SERVER_AUTH)
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            switch (context.Request.Url.LocalPath)
            {
                case "/loadtracks":
                    var resp = await LoadTracks(context.Request.QueryString);
                    await context.Response.OutputStream.WriteAsync(resp.Response);
                    context.Response.StatusCode = resp.StatusCode;
                    context.Response.Close();
                    return;
                case "/":
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        return;
                    }

                    WebSocketContext webSocketContext = null;
                    try
                    {
                        webSocketContext = await context.AcceptWebSocketAsync(null);
                        Interlocked.Increment(ref count);
                        Console.WriteLine("Processed: {0}", count);
                    }
                    catch (Exception e)
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                        Console.WriteLine("Exception: {0}", e);
                        return;
                    }

                    var _ = new LavaSocketConnection(webSocketContext.WebSocket, int.Parse(context.Request.Headers.Get("Num-Shards")), ulong.Parse(context.Request.Headers.Get("User-Id"))).HandleAsync().ConfigureAwait(false);
                    return;
                default:
                    Console.WriteLine("unknown: " + context.Request.Url.LocalPath);
                    return;
            }
        }

        private async Task<(ReadOnlyMemory<byte> Response, int StatusCode)> LoadTracks(NameValueCollection query)
        {
            string identifier = query.Get("identifier");
            if (identifier == null)
            {
                return (Encoding.UTF8.GetBytes("No identifier"), 400);
            }
            
            if (identifier.StartsWith("ytsearch:"))
            {
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = LoadTracksResp.LoadType.SEARCH_RESULT,
                    tracks = (await Youtube.Search(identifier.Substring(9))).Select(track => new LoadTracksResp.TrackObj() { info = track }).ToArray()
                })), 200);
            }

            return (new ReadOnlyMemory<byte>(), 500);
        }

        struct LoadTracksResp
        {
            public PlaylistInfo playlistInfo;
            public LoadType loadType;
            public TrackObj[] tracks;

            public struct PlaylistInfo
            {

            }

            public enum LoadType
            {
                SEARCH_RESULT
            }

            public struct TrackObj
            {
                public string track;
                public LavaTrack info;
            }
        }
    }
}
