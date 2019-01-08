using Lava.Net.Sources.Youtube;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

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
            listener.Prefixes.Add(ListenerUri);
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
            Console.WriteLine("Recieved " + context.Request.Url.LocalPath);
            switch (context.Request.Url.LocalPath)
            {
                case "/loadtracks":
                    var resp = await LoadTracks(context.Request.QueryString.Get("identifier"));
                    Console.WriteLine(Encoding.UTF8.GetString(resp.Response.ToArray()));
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

                    var _ = new LavaSocketConnection(webSocketContext.WebSocket, int.Parse(context.Request.Headers.Get("Num-Shards")), ulong.Parse(context.Request.Headers.Get("User-Id"))).HandleAsync().ConfigureAwait(false);
                    return;
                default:
                    Console.WriteLine("unknown: " + context.Request.Url.LocalPath);
                    return;
            }
        }
        /*
{
	"playlistInfo": {},
	"loadType": "TRACK_LOADED",
	"tracks": [{
		"track": "QAAAeAIAEzEgc2Vjb25kIGxvbmcgdmlkZW8AC3BoYXRyb2JzaG93AAAAAAAAA+gAC09tUDFpWmwxZ0g4AAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9T21QMWlabDFnSDgAB3lvdXR1YmUAAAAAAAAAAA==",
		"info": {
			"identifier": "OmP1iZl1gH8",
			"isSeekable": true,
			"author": "phatrobshow",
			"length": 1000,
			"isStream": false,
			"position": 0,
			"title": "1 second long video",
			"uri": "https://www.youtube.com/watch?v=OmP1iZl1gH8"
		}
	}]
}
*/
        private async Task<(ReadOnlyMemory<byte> Response, int StatusCode)> LoadTracks(string identifier)
        {
            if (identifier == null)
            {
                return (Encoding.UTF8.GetBytes("No identifier"), 400);
            }
            
            if (identifier.StartsWith("ytsearch:"))
            {
                var result = await Youtube.Search(identifier.Substring(9));
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = result.Length == 0 ? LoadTracksResp.LoadType.NO_MATCHES : LoadTracksResp.LoadType.SEARCH_RESULT,
                    tracks = result.Select(track => new LoadTracksResp.TrackObj() { track = track.Track, info = track }).ToArray()
                })), 200);
            }
            else
            {
                var track = await Youtube.GetTrack(identifier);
                return (Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new LoadTracksResp()
                {
                    loadType = track == default ? LoadTracksResp.LoadType.NO_MATCHES : LoadTracksResp.LoadType.TRACK_LOADED,
                    tracks = new LoadTracksResp.TrackObj[] { new LoadTracksResp.TrackObj() { track = track.Track, info = track } }
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

            public enum LoadType
            {
                TRACK_LOADED,
                PLAYLIST_LOADED,
                SEARCH_RESULT,
                NO_MATCHES,
                LOAD_FAILED
            }

            public struct TrackObj
            {
                public string track;
                public LavaTrack info;
            }
        }
    }
}
