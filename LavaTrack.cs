using Newtonsoft.Json;

namespace Lava.Net
{
    public class LavaTrack
    {
        //public string Track; Lavalink uses this internally.

        [JsonProperty("identifier")]
        public string Identifier;

        [JsonProperty("isSeekable")]
        public bool Seekable;

        [JsonProperty("author")]
        public string Author;

        [JsonProperty("length")]
        public int Length;

        [JsonProperty("isStream")]
        public bool Stream;

        [JsonProperty("position")]
        public int Position;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("uri")]
        public string Uri;
    }
}
