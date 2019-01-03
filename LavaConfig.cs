using Newtonsoft.Json;
using System.IO;

namespace Lava.Net
{
    public sealed class LavaConfig
    {
        static LavaConfig()
        {
            JsonConvert.DeserializeObject<LavaConfig>(File.ReadAllText("../../../config.json"));
        }

        public static string SERVER_URI => $"{SERVER_ADDRESS}:{SERVER_PORT}";

        // SERVER
        [JsonProperty("server/port")]
        public static readonly ushort SERVER_PORT;

        [JsonProperty("server/address")]
        public static readonly string SERVER_ADDRESS;
        
        [JsonProperty("server/password")]
        public static readonly string SERVER_AUTH;

        // SOURCES
        [JsonProperty("sources/youtube")]
        public static readonly bool SOURCES_YOUTUBE;

        [JsonProperty("sources/bandcamp")]
        public static readonly bool SOURCES_BANDCAMP;

        [JsonProperty("sources/soundcloud")]
        public static readonly bool SOURCES_SOUNDCLOUD;

        [JsonProperty("sources/twitch")]
        public static readonly bool SOURCES_TWITCH;

        [JsonProperty("sources/vimeo")]
        public static readonly bool SOURCES_VIMEO;

        [JsonProperty("sources/mixer")]
        public static readonly bool SOURCES_MIXER;

        [JsonProperty("sources/http")]
        public static readonly bool SOURCES_HTTP;

        [JsonProperty("sources/local")]
        public static readonly bool SOURCES_LOCAL;
    }
}
