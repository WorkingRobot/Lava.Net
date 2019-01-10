using HtmlAgilityPack;
using Lava.Net.Streams;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Lava.Net.Sources.Youtube
{
    internal static class Youtube
    {
        private static HttpClient Client = new HttpClient();

        public static async Task<(List<LavaTrack> tracks, LoadType type)> Search(string query)
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
                        track.Track = "yt:" + track.Identifier;
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
                        track.Length = duration * 1000;
                        track.Stream = false;
                    }
                }
                if (!string.IsNullOrWhiteSpace(track.Identifier))
                    tracks.Add(track);
            }
            return (tracks, tracks.Count == 0 ? LoadType.NO_MATCHES : LoadType.SEARCH_RESULT);
        }

        public static async Task<(LavaTrack track, LoadType type)> GetTrack(string identifier)
        {
            JObject json = JObject.Parse((await Client.GetStringAsync("https://www.youtube.com/watch?v=" + identifier)).Split(";ytplayer.config = ", 2)[1].Split(";ytplayer.load", 2)[0]);
            try
            {
                return (new LavaTrack()
                {
                    Author = json["args"]["author"].ToString(),
                    Identifier = identifier,
                    Length = json["args"]["length_seconds"].ToObject<int>() * 1000,
                    Seekable = true,
                    Stream = json["args"].Value<int>("livestream") == 1,
                    Title = json["args"]["title"].ToString(),
                    Uri = "https://www.youtube.com/watch?v=" + identifier,
                    Track = "yt:" + identifier
                }, LoadType.TRACK_LOADED);
            }
            catch (NullReferenceException)
            {
                return (null, LoadType.NO_MATCHES);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return (null, LoadType.LOAD_FAILED);
            }
        }
        
        public static Task<LavaStream> GetStream(LavaTrack track) => GetStream(track.Identifier);

        static Dictionary<string, string> CachedPlayers = new Dictionary<string, string>();
        public static async Task<LavaStream> GetStream(string identifier)
        {
            var json = JObject.Parse((await Client.GetStringAsync("https://www.youtube.com/watch?v=" + identifier)).Split(";ytplayer.config = ", 2)[1].Split(";ytplayer.load", 2)[0]);
            if (!CachedPlayers.TryGetValue(json["assets"]["js"].ToString(), out string player_js)) // Used to decipher encrypted signatures
            {
                player_js = CachedPlayers[json["assets"]["js"].ToString()] = await Client.GetStringAsync("https://www.youtube.com" + json["assets"]["js"].ToString());
            }

            var fmts = json["args"]["adaptive_fmts"].ToString().Split(",") // Get formats and split by the delimiter
                .Select(format => format.Split("&").ToDictionary(param => param.Split("=", 2)[0], param => param.Split("=", 2)[1])) // Convert the query to a dictionary
                .Select(format => new StreamInfo(format["type"], format["url"], uint.Parse(format["bitrate"]), format.ContainsKey("s") ? format["s"] : format.GetValueOrDefault("sig"), format.ContainsKey("s"))); // Convert the format to a better form
            fmts = fmts.Where(format => format.Type.ToString().Contains("AUDIO")); // Only look at audio streams
            if (fmts.Count() == 0)
            {
                throw new NotImplementedException("No audio formats.");
            }
            Console.WriteLine(fmts.Count() + " audio formats");
            //fmts = fmts.Where(format => format.Codec == StreamCodec.OPUS); // Only look at opus streams
            if (fmts.Count() == 0)
            {
                throw new NotImplementedException("No opus formats.");
            }
            Console.WriteLine(fmts.Count() + " opus formats");
            var fmt = fmts.MinBy(format => format.Bitrate); // Get the one with the lowest bitrate

            StringBuilder url = new StringBuilder(HttpUtility.UrlDecode(fmt.Url));
            if (!string.IsNullOrWhiteSpace(fmt.Signature)) // Unencrypted signature
            {
                url.Append("&signature=");
                if (fmt.SignatureEncrypted)
                {
                    url.Append(DecryptSignature(fmt.Signature, player_js));
                }
                else
                    url.Append(fmt.Signature);
            }
            if (!fmt.Url.Contains("ratebypass")) // Add if doesn't exist
            {
                url.Append("&ratebypass=yes");
            }
            return new LavaStream(await Utils.GetStream(url.ToString()));
        }


        private static readonly Regex DecryptionFunctionRegex = new Regex(@"\bc\s*&&\s*d\.set\([^,]+\s*,\s*\([^)]*\)\s*\(\s*([a-zA-Z0-9$]+)\(");
        private static readonly Regex FunctionRegex = new Regex(@"\w+(?:.|\[)(\""?\w+(?:\"")?)\]?\(");

        private static string DecryptSignature(string signature, string js)
        {
            var functionLines = GetDecryptionFunctionLines(js);

            var decryptor = new Decryptor();
            foreach (var functionLine in functionLines)
            {
                if (decryptor.IsComplete)
                {
                    break;
                }

                var match = FunctionRegex.Match(functionLine);
                if (match.Success)
                {
                    decryptor.AddFunction(js, match.Groups[1].Value);
                }
            }

            foreach (var functionLine in functionLines)
            {
                var match = FunctionRegex.Match(functionLine);
                if (match.Success)
                {
                    signature = decryptor.ExecuteFunction(signature, functionLine, match.Groups[1].Value);
                }
            }

            return signature;
        }

        private static string[] GetDecryptionFunctionLines(string js)
        {
            var decryptionFunction = GetDecryptionFunction(js);
            var match =
                Regex.Match(
                    js,
                    $@"(?!h\.){Regex.Escape(decryptionFunction)}=function\(\w+\)\{{(.*?)\}}",
                    RegexOptions.Singleline);
            if (!match.Success)
            {
                throw new Exception($"{nameof(GetDecryptionFunctionLines)} failed");
            }

            return match.Groups[1].Value.Split(';');
        }

        private static string GetDecryptionFunction(string js)
        {
            var match = DecryptionFunctionRegex.Match(js);
            if (!match.Success)
            {
                throw new Exception($"{nameof(GetDecryptionFunction)} failed");
            }

            return match.Groups[1].Value;
        }

        private class Decryptor
        {
            private static readonly Regex ParametersRegex = new Regex(@"\(\w+,(\d+)\)");

            private readonly Dictionary<string, FunctionType> _functionTypes = new Dictionary<string, FunctionType>();
            private readonly StringBuilder _stringBuilder = new StringBuilder();

            public bool IsComplete =>
                _functionTypes.Count == Enum.GetValues(typeof(FunctionType)).Length;

            public void AddFunction(string js, string function)
            {
                var escapedFunction = Regex.Escape(function);
                FunctionType? type = null;
                if (Regex.IsMatch(js, $@"{escapedFunction}:\bfunction\b\(\w+\)"))
                {
                    type = FunctionType.Reverse;
                }
                else if (Regex.IsMatch(js, $@"{escapedFunction}:\bfunction\b\([a],b\).(\breturn\b)?.?\w+\."))
                {
                    type = FunctionType.Slice;
                }
                else if (Regex.IsMatch(js, $@"{escapedFunction}:\bfunction\b\(\w+\,\w\).\bvar\b.\bc=a\b"))
                {
                    type = FunctionType.Swap;
                }

                if (type.HasValue)
                {
                    _functionTypes[function] = type.Value;
                }
            }

            public string ExecuteFunction(string signature, string line, string function)
            {
                if (!_functionTypes.TryGetValue(function, out var type))
                {
                    return signature;
                }

                switch (type)
                {
                    case FunctionType.Reverse:
                        return Reverse(signature);
                    case FunctionType.Slice:
                    case FunctionType.Swap:
                        var index =
                            int.Parse(
                                ParametersRegex.Match(line).Groups[1].Value,
                                NumberStyles.AllowThousands,
                                NumberFormatInfo.InvariantInfo);
                        return
                            type == FunctionType.Slice
                                ? Slice(signature, index)
                                : Swap(signature, index);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type));
                }
            }

            private string Reverse(string signature)
            {
                _stringBuilder.Clear();
                for (var index = signature.Length - 1; index >= 0; index--)
                {
                    _stringBuilder.Append(signature[index]);
                }

                return _stringBuilder.ToString();
            }

            private string Slice(string signature, int index) =>
                signature.Substring(index);

            private string Swap(string signature, int index)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(signature);
                _stringBuilder[0] = signature[index];
                _stringBuilder[index] = signature[0];
                return _stringBuilder.ToString();
            }

            private enum FunctionType
            {
                Reverse,
                Slice,
                Swap
            }
        }
    }
}
