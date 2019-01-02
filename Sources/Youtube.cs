using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace Lava.Net.Sources
{
    static class Youtube
    {
        private const string API_KEY = @"lol no";

        private static HttpClient client = new HttpClient();

        // will be replaced because i dont like the api lol
        public static async Task<LavaTrack> GetTrack(string identifier)
        {
            var resp = JObject.Parse(await client.GetStringAsync($@"https://www.googleapis.com/youtube/v3/videos?part=contentDetails%2Csnippet&id={identifier}&key={API_KEY}"))["items"][0];
            return new LavaTrack()
            {
                Author = resp["snippet"]["channelTitle"].ToString(),
                Identifier = identifier,
                Length = (int)XmlConvert.ToTimeSpan(resp["contentDetails"]["duration"].ToString()).TotalSeconds,
                Seekable = true,
                Stream = resp["snippet"]["liveBroadcastContent"].ToString() != "none",
                Title = resp["snippet"]["title"].ToString(),
                Uri = "https://www.youtube.com/watch?v="+identifier
            };
        }

        public static async Task<LavaTrack[]> Search(string query)
        {
            
            List<LavaTrack> tracks = new List<LavaTrack>();
            var doc = new HtmlDocument();
            doc.LoadHtml(await client.GetStringAsync("https://www.youtube.com/results?search_query=" + query));
            foreach (HtmlNode video in doc.DocumentNode.Descendants().Where(n => n.NodeType == HtmlNodeType.Element).Where(e => e.Name == "div" && e.GetAttributeValue("class", "").Contains("yt-lockup-content")))
            {
                LavaTrack track = new LavaTrack()
                {
                    Seekable = true,
                    Stream = true
                };
                foreach (var desc in video.Descendants())
                {
                    if (desc.ParentNode.GetAttributeValue("class", "").StartsWith("yt-lockup-byline") && desc.Name == "a")
                    {
                        track.Author = desc.InnerHtml;
                    }
                    else if (desc.Name == "a" && desc.GetAttributeValue("href", "").StartsWith("/watch?v="))
                    {
                        track.Identifier = desc.GetAttributeValue("href", "").Split('=')[1];
                        track.Uri = "https://www.youtube.com/watch?v=" + track.Identifier;
                        track.Title = desc.GetAttributeValue("title", "");
                    }
                    else if (desc.Name == "span" && desc.GetAttributeValue("class","") == "accessible-description")
                    {
                        if (!desc.InnerHtml.Contains("Duration: ")) continue;
                        var duration = 0;
                        foreach (string part in desc.InnerHtml.Split("Duration: ")[1].Split('.')[0].Split(':'))
                        {
                            duration = duration * 60 + int.Parse(part);
                        }
                        track.Length = duration;
                        track.Stream = false;
                    }
                }
                if (!string.IsNullOrWhiteSpace(track.Identifier))
                    tracks.Add(track);
            }
            return tracks.ToArray();
        }
    }
}
