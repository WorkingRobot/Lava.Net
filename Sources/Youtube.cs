using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Lava.Net.Sources
{
    static class Youtube
    {
        private static HttpClient Client = new HttpClient();

        public static async Task<LavaTrack[]> Search(string query)
        {
            List<LavaTrack> tracks = new List<LavaTrack>();
            var doc = new HtmlDocument();
            doc.LoadHtml(await Client.GetStringAsync("https://www.youtube.com/results?search_query=" + query));
            foreach (HtmlNode video in doc.DocumentNode.Descendants()
                .Where(n => n.NodeType == HtmlNodeType.Element && // Elements only
                n.Name == "div" && // Divs only
                n.GetAttributeValue("class", "").Contains("yt-lockup-content"))) // Youtube videos
            {
                LavaTrack track = new LavaTrack()
                {
                    Seekable = true,
                    Stream = true
                };
                foreach (var desc in video.Descendants())
                {
                    if (desc.ParentNode.GetAttributeValue("class", "").StartsWith("yt-lockup-byline") && desc.Name == "a") // a tag that leads to the channel (we just want the name)
                    {
                        track.Author = desc.InnerHtml;
                    }
                    else if (desc.Name == "a" && desc.GetAttributeValue("href", "").StartsWith("/watch?v=")) // a tag that contains the URL to the video and the title
                    {
                        track.Identifier = desc.GetAttributeValue("href", "").Split('=')[1];
                        track.Uri = "https://www.youtube.com/watch?v=" + track.Identifier;
                        track.Title = desc.GetAttributeValue("title", "");
                    }
                    else if (desc.Name == "span" && desc.GetAttributeValue("class","") == "accessible-description") // span tag that contains the duration of the video
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
