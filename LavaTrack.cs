using Newtonsoft.Json;

namespace Lava.Net
{
    public class LavaTrack
    {
        [JsonIgnore]
        public string Track; // Lavalink uses this internally. I use it differently.

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
