using Newtonsoft.Json;
using System.IO;

namespace Lava.Net
{
    /// <summary>
    /// Configuration class for Lava.Net. Reads values from config.json when initialized.
    /// </summary>
    public sealed class LavaConfig
    {
        static LavaConfig()
        {
            JsonConvert.DeserializeObject<LavaConfig>(File.ReadAllText("config.json"));
        }
        
        [JsonProperty("server")]
        public static readonly ServerConfig Server;

        public struct ServerConfig
        {
            public string Uri => $"{Server.Address}:{Server.Port}";

            [JsonProperty("address")]
            public readonly string Address;

            [JsonProperty("port")]
            public readonly ushort Port;

            [JsonProperty("password")]
            public readonly string Authorization;
        }
        
        [JsonProperty("sources")]
        public static readonly SourcesConfig Sources;

        public struct SourcesConfig
        {
            [JsonProperty("youtube")]
            public readonly bool Youtube;

            [JsonProperty("bandcamp")]
            public readonly bool Bandcamp;

            [JsonProperty("soundcloud")]
            public readonly bool Soundcloud;

            [JsonProperty("twitch")]
            public readonly bool Twitch;

            [JsonProperty("vimeo")]
            public readonly bool Vimeo;

            [JsonProperty("mixer")]
            public readonly bool Mixer;

            [JsonProperty("http")]
            public readonly bool Http;

            [JsonProperty("local")]
            public readonly bool Local;
        }
    }
}
