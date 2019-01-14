using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Lava.Net.Streams;
using Newtonsoft.Json;

namespace Lava.Net.Sources.Soundcloud
{
    internal class Soundcloud : ISource
    {
        private static HttpClient Client;

        // Currently can't find anyway that Lavalink can load soundcloud tracks without scsearch
        public bool ValidateTrack(string identifier) => false;

        static Soundcloud()
        {
            Client = new HttpClient();
            Client.DefaultRequestHeaders.Add("User-Agent", "Lava.Net"); // Otherwise returns a 401 Unauthorized
        }

        public async Task<(List<LavaTrack> tracks, LoadType type)> Search(string query)
        {
            SCTrack[] scTracks;
            try
            {
                scTracks = JsonConvert.DeserializeObject<SCSearchResp>(await Client.GetStringAsync($"https://api-v2.soundcloud.com/search/tracks?q={HttpUtility.UrlEncode(query)}&client_id={await GetClientID()}")).collection;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return (null, LoadType.LOAD_FAILED);
            }
            if (scTracks.Length == 0)
            {
                return (null, LoadType.NO_MATCHES);
            }
            return (scTracks.Select(scTrack => ConvertSCTrack(scTrack)).ToList(), LoadType.SEARCH_RESULT);
        }

        public async Task<(LavaTrack track, LoadType type)> GetTrack(string identifier)
        {
            try
            {
                return (ConvertSCTrack(await GetSCTrack(identifier)), LoadType.TRACK_LOADED);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // unsure which exceptions throw a fail yet
                return (null, LoadType.NO_MATCHES);
            }
        }

        public Task<LavaStream> GetStream(LavaTrack track) => GetStream(track.Identifier);

        public async Task<LavaStream> GetStream(string identifier)
        {
            string url = JsonConvert.DeserializeObject<SCStreams>(await Client.GetStringAsync($"https://api.soundcloud.com/tracks/{identifier}/streams?client_id={await GetClientID()}")).MP3_URL;
            return await LavaStream.FromUrl(url);
        }

        string clientId;
        async Task<string> GetClientID()
        {
            if (clientId == null)
            {
                string client_js_url = (await Client.GetStringAsync("https://soundcloud.com"))
                    .Split("https://a-v2.sndcdn.com/assets/app-")[1].Split(".js")[0]; // Find the url that links to the javascript
                clientId = (await Client.GetStringAsync($"https://a-v2.sndcdn.com/assets/app-{client_js_url}.js"))
                    .Split("client_id:\"")[1].Split("\"")[0]; // Get the client id in the javascript
            }
            return clientId;
        }

        async Task<SCTrack> GetSCTrack(string identifier) =>
            JsonConvert.DeserializeObject<SCTrack>(await Client.GetStringAsync($"https://api-v2.soundcloud.com/tracks/{identifier}?client_id={await GetClientID()}"));

        LavaTrack ConvertSCTrack(SCTrack track) =>
            new LavaTrack()
            {
                Author = track.Author.Name,
                Identifier = track.Identifier,
                Length = track.Length,
                Seekable = true,
                Stream = false,
                Title = track.Title,
                Track = "sc:" + track.Identifier,
                Uri = track.Uri
            };

        struct SCSearchResp
        {
            public SCTrack[] collection;
        }

        struct SCTrack
        {
            [JsonProperty("id")]
            public string Identifier;

            [JsonProperty("user")]
            public SCAuthor Author;

            [JsonProperty("duration")]
            public int Length;

            [JsonProperty("title")]
            public string Title;

            [JsonProperty("permalink_url")]
            public string Uri;
        }

        struct SCAuthor
        {
            [JsonProperty("username")]
            public string Name;
        }

        struct SCStreams
        {
            [JsonProperty("http_mp3_128_url")]
            public string MP3_URL;
        }
    }
}
