using Lava.Net.Sources;
using Lava.Net.Sources.Youtube;
using Lava.Net.Sources.Soundcloud;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Lava.Net
{
    class LavaSocketServer
    {
        private readonly string ListenerUri;

        public LavaSocketServer(string listenerUri)
        {
            ListenerUri = listenerUri;
        }

        public async Task StartAsync()
        {
            HttpListener listener = new HttpListener();
            Console.WriteLine("Listening to " + ListenerUri);
            listener.Prefixes.Add(ListenerUri);
            listener.Start();

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                var _ = ProcessRequest(context).ConfigureAwait(false);
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            if (context.Request.Headers.Get("Authorization") != LavaConfig.Server.Authorization)
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }
            switch (context.Request.Url.LocalPath)
            {
                case "/loadtracks":
                    var resp = await LoadTracks(context.Request.QueryString.Get("identifier"));
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
                    }
                    catch (Exception e)
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                        Console.WriteLine("Exception: {0}", e);
                        return;
                    }

                    var _ = Task.Factory.StartNew(
                        function: new LavaSocketConnection(webSocketContext.WebSocket, int.Parse(context.Request.Headers.Get("Num-Shards")), ulong.Parse(context.Request.Headers.Get("User-Id"))).HandleAsync,
                        cancellationToken: CancellationToken.None,
                        creationOptions: TaskCreationOptions.LongRunning,
                        scheduler: TaskScheduler.Default).ConfigureAwait(false);
                    return;
                default:
                    Console.WriteLine("Unknown Path Requested: " + context.Request.Url.LocalPath);
                    return;
            }
        }

        internal static SortedDictionary<string, ISource> Sources = new SortedDictionary<string, ISource>()
        {
            { "youtube", new Youtube() },
            { "soundcloud", new Soundcloud() }
        };

        private async Task<(ReadOnlyMemory<byte> Response, int StatusCode)> LoadTracks(string identifier)
        {
            if (identifier == null)
            {
                return (Encoding.UTF8.GetBytes("No identifier"), 400);
            }
            
            if (LavaConfig.Sources.Youtube && identifier.StartsWith("ytsearch:"))
            {
                var result = await Sources["youtube"].Search(identifier.Substring(9));
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = result.type,
                    tracks = result.tracks.Select(track => new LoadTracksResp.TrackObj() { track = track.Track, info = track }).ToArray()
                })), 200);
            }
            else if (LavaConfig.Sources.Soundcloud && identifier.StartsWith("scsearch:"))
            {
                var result = await Sources["soundcloud"].Search(identifier.Substring(9));
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = result.type,
                    tracks = result.tracks.Select(track => new LoadTracksResp.TrackObj() { track = track.Track, info = track }).ToArray()
                })), 200);
            }
            else if (LavaConfig.Sources.Youtube && Sources["youtube"].ValidateTrack(identifier))
            {
                var result = await Sources["youtube"].GetTrack(identifier);
                LoadTracksResp.TrackObj[] tracks;
                if (result.track == null)
                {
                    tracks = new LoadTracksResp.TrackObj[0];
                }
                else
                {
                    tracks = new LoadTracksResp.TrackObj[] { new LoadTracksResp.TrackObj() { track = result.track.Track, info = result.track } };
                }
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = result.type,
                    tracks = tracks
                })), 200);
            }
            else
            {
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = LoadType.NO_MATCHES,
                    tracks = new LoadTracksResp.TrackObj[0]
                })), 200);
            }

            return (new ReadOnlyMemory<byte>(), 500);
        }

        struct LoadTracksResp
        {
            public PlaylistInfo playlistInfo;
            [JsonConverter(typeof(StringEnumConverter))]
            public LoadType loadType;
            public TrackObj[] tracks;

            public struct PlaylistInfo
            {
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public string name;
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
                public int selectedTrack;
            }

            public struct TrackObj
            {
                public string track;
                public LavaTrack info;
            }
        }
    }
    
    public enum LoadType
    {
        TRACK_LOADED,
        PLAYLIST_LOADED,
        SEARCH_RESULT,
        NO_MATCHES,
        LOAD_FAILED
    }
}
