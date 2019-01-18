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
    internal class Youtube : ISource
    {
        private static HttpClient Client = new HttpClient();

        private static readonly Regex VideoIdRegex = new Regex(@"^[a-zA-Z0-9_-]{11}$");
        public bool ValidateTrack(string identifier) => VideoIdRegex.IsMatch(identifier);

        public async Task<(List<LavaTrack> tracks, LoadType type)> Search(string query)
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

        public async Task<(LavaTrack track, LoadType type)> GetTrack(string identifier)
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
                Console.WriteLine(e);
                return (null, LoadType.LOAD_FAILED);
            }
        }
        
        public Task<LavaStream> GetStream(LavaTrack track) => GetStream(track.Identifier);

        static Dictionary<string, CipherOperation[]> CachedPlayers = new Dictionary<string, CipherOperation[]>();

        // TODO: Rework picking the best stream.
        public async Task<LavaStream> GetStream(string identifier)
        {
            var json = JObject.Parse((await Client.GetStringAsync("https://www.youtube.com/watch?v=" + identifier)).Split(";ytplayer.config = ", 2)[1].Split(";ytplayer.load", 2)[0]);
            if (!CachedPlayers.TryGetValue(json["assets"]["js"].ToString(), out CipherOperation[] ops)) // Used to decipher encrypted signatures
            {
                ops = CachedPlayers[json["assets"]["js"].ToString()] = GetCipher(await Client.GetStringAsync("https://www.youtube.com" + json["assets"]["js"].ToString()));
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
            /*fmts = fmts.Where(format => format.Codec == StreamCodec.OPUS); // Only look at opus streams
            if (fmts.Count() == 0)
            {
                throw new NotImplementedException("No opus formats.");
            }
            Console.WriteLine(fmts.Count() + " opus formats");*/
            var fmt = fmts.MinBy(format => format.Bitrate); // Get the one with the lowest bitrate

            StringBuilder url = new StringBuilder(HttpUtility.UrlDecode(fmt.Url));
            if (!string.IsNullOrWhiteSpace(fmt.Signature)) // Unencrypted signature
            {
                url.Append("&signature=");
                if (fmt.SignatureEncrypted)
                {
                    url.Append(DecryptSignature(fmt.Signature, ops));
                }
                else
                    url.Append(fmt.Signature);
            }
            if (!fmt.Url.Contains("ratebypass")) // Add if doesn't exist
            {
                url.Append("&ratebypass=yes");
            }
            return await LavaStream.FromUrl(url.ToString());
        }

        // Below decrypts encrypted signatures in stream URLs

        private const string VARIABLE_PART = @"[a-zA-Z_\$][a-zA-Z_0-9]*";
        private const string VARIABLE_PART_DEFINE = @"\""?" + VARIABLE_PART + @"\""?";
        private const string BEFORE_ACCESS = @"(?:\[\"" |\.)";
        private const string AFTER_ACCESS = @"(?:\""\]|)";
        private const string VARIABLE_PART_ACCESS = BEFORE_ACCESS + VARIABLE_PART + AFTER_ACCESS;
        private const string REVERSE_PART = @":function\(a\)\{(?:return )?a\.reverse\(\)\}";
        private const string SLICE_PART = @":function\(a,b\)\{return a\.slice\(b\)\}";
        private const string SPLICE_PART = @":function\(a,b\)\{a\.splice\(0,b\)\}";
        private const string SWAP_PART = @":function\(a,b\)\{var c=a\[0\];a\[0\]=a\[b%a\.length\];a\[b(?:%a.length|)\]=c(?:;return a)?\}";

        private static Regex functionPattern = new Regex(
            "function(?: " + VARIABLE_PART + @")?\(a\)\{" +
            @"a=a\.split\(""""\);\s*" +
            "((?:(?:a=)?" + VARIABLE_PART + VARIABLE_PART_ACCESS + @"\(a,\d+\);)+)" +
            @"return a\.join\(""""\)" +
            @"\}"
        );

        private static Regex actionsPattern = new Regex(
            "var (" + VARIABLE_PART + ")=\\{((?:(?:" +
            VARIABLE_PART_DEFINE + REVERSE_PART + "|" +
            VARIABLE_PART_DEFINE + SLICE_PART + "|" +
            VARIABLE_PART_DEFINE + SPLICE_PART + "|" +
            VARIABLE_PART_DEFINE + SWAP_PART +
            @"),?\n?)+)\};"
        );

        private const string PATTERN_PREFIX = @"(?:^|,)\""?(" + VARIABLE_PART + @")\""?";

        private static Regex reversePattern = new Regex(PATTERN_PREFIX + REVERSE_PART, RegexOptions.Multiline);
        private static Regex slicePattern = new Regex(PATTERN_PREFIX + SLICE_PART, RegexOptions.Multiline);
        private static Regex splicePattern = new Regex(PATTERN_PREFIX + SPLICE_PART, RegexOptions.Multiline);
        private static Regex swapPattern = new Regex(PATTERN_PREFIX + SWAP_PART, RegexOptions.Multiline);

        private CipherOperation[] GetCipher(string script)
        {
            var actions = actionsPattern.Match(script).Groups;
            if (actions.Count == 0)
            {
                throw new ArgumentException("Must find action functions from script");
            }

            string actionBody = actions[2].Value;

            string reverseKey = ExtractDollarEscapedFirstGroup(reversePattern, actionBody);
            string slicePart = ExtractDollarEscapedFirstGroup(slicePattern, actionBody);
            string splicePart = ExtractDollarEscapedFirstGroup(splicePattern, actionBody);
            string swapKey = ExtractDollarEscapedFirstGroup(swapPattern, actionBody);

            var extractor = new Regex(
                "(?:a=)?" + Regex.Escape(actions[1].Value) + BEFORE_ACCESS + "(" +
                string.Join("|", GetQuotedFunctions(reverseKey, slicePart, splicePart, swapKey)) +
                ")" + AFTER_ACCESS + "\\(a,(\\d+)\\)"
            );

            var functions = functionPattern.Match(script).Groups;
            if (functions.Count == 0)
            {
                throw new ArgumentException("Must find decipher function from script");
            }

            var matcher = extractor.Matches(functions[1].Value);
            List<CipherOperation> ops = new List<CipherOperation>();
            foreach (Match m in matcher)
            {
                string type = m.Groups[1].Value;
                if (type == swapKey)
                {
                    ops.Add(new CipherOperation(OperationType.SWAP, int.Parse(m.Groups[2].Value)));
                }
                else if (type == reverseKey)
                {
                    ops.Add(new CipherOperation(OperationType.REVERSE));
                }
                else if (type == slicePart)
                {
                    ops.Add(new CipherOperation(OperationType.SLICE, int.Parse(m.Groups[2].Value)));
                }
                else if (type == splicePart)
                {
                    ops.Add(new CipherOperation(OperationType.SPLICE, int.Parse(m.Groups[2].Value)));
                }
                else
                {
                    throw new ArgumentException("Unknown cipher function " + type);
                }
            }

            if (ops.Count == 0)
            {
                throw new ArgumentException("No operations detected from cipher extracted");
            }

            return ops.ToArray();
        }

        private string DecryptSignature(string signature, CipherOperation[] ops)
        {
            StringBuilder builder = new StringBuilder(signature);
            foreach (var op in ops)
            {
                switch (op.Type)
                {
                    case OperationType.SWAP:
                        int position = op.Param % builder.Length;
                        char temp = builder[0];
                        builder[0] = builder[position];
                        builder[position] = temp;
                        break;
                    case OperationType.REVERSE:
                        char[] array = new char[builder.Length];
                        builder.CopyTo(0, array, 0, builder.Length);
                        Array.Reverse(array);
                        builder = new StringBuilder(new string(array));
                        break;
                    case OperationType.SLICE:
                    case OperationType.SPLICE:
                        builder.Remove(0, op.Param);
                        break;
                }
            }
            return builder.ToString();
        }

        private static string ExtractDollarEscapedFirstGroup(Regex pattern, string text)
        {
            var matcher = pattern.Match(text).Groups;
            return matcher.Count != 0 ? matcher[1].Value.Replace("$", "\\$") : null;
        }

        private static IEnumerable<string> GetQuotedFunctions(params string[] functionNames)
        {
            return functionNames
                .Where(func => !string.IsNullOrWhiteSpace(func))
                .Select(func => Regex.Escape(func));
        }

        public struct CipherOperation
        {
            public OperationType Type;
            public int Param;

            public CipherOperation(OperationType type, int param = 0)
            {
                Type = type;
                Param = param;
            }
        }

        public enum OperationType
        {
            SWAP,
            REVERSE,
            SLICE,
            SPLICE
        }
    }
}
