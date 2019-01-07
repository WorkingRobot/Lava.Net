using System;
using System.Web;

namespace Lava.Net.Sources.Youtube
{
    struct StreamInfo
    {
        public StreamType Type;
        public StreamCodec Codec;
        public uint Bitrate;
        public string Url;
        public string Signature;
        public bool SignatureEncrypted;

        public StreamInfo(string type, string url, uint bitrate, string signature, bool sigEnc) // type e.g: audio/mp4; codecs="mp4a.40.2"
        {
            string[] split = HttpUtility.UrlDecode(type).Split("; codecs=");
            switch (split[0])
            {
                case "video/mp4":
                    Type = StreamType.MP4_VIDEO;
                    break;
                case "audio/mp4":
                    Type = StreamType.MP4_AUDIO;
                    break;
                case "text/mp4":
                    Type = StreamType.MP4_TEXT;
                    break;
                case "video/webm":
                    Type = StreamType.WEBM_VIDEO;
                    break;
                case "audio/webm":
                    Type = StreamType.WEBM_AUDIO;
                    break;
                default:
                    Type = default;
                    break;
            }
            switch (split[1].Split('"')[1].Split('.')[0]) // "Versioning" isn't really an issue
            {
                case "avc":
                case "avc1":
                    Codec = StreamCodec.AVC;
                    break;
                case "opus":
                    Codec = StreamCodec.OPUS;
                    break;
                case "vorbis":
                    Codec = StreamCodec.VORBIS;
                    break;
                case "vp9":
                    Codec = StreamCodec.VP9;
                    break;
                case "mp4a":
                    Codec = StreamCodec.MP4A;
                    break;
                case "unknown":
                    Codec = StreamCodec.UNKNOWN;
                    break;
                default:
                    Codec = default;
                    break;
            }
            Url = url;
            Bitrate = bitrate;
            Signature = signature;
            SignatureEncrypted = sigEnc;
        }

        public StreamInfo(StreamType type, StreamCodec codec, string url, uint bitrate, string signature, bool sigEnc)
        {
            Type = type;
            Codec = codec;
            Url = url;
            Bitrate = bitrate;
            Signature = signature;
            SignatureEncrypted = sigEnc;
        }
    }

    enum StreamType
    {
        NONE,
        MP4_VIDEO,
        MP4_AUDIO,
        MP4_TEXT, // Probably a stream m3u playlist
        WEBM_VIDEO,
        WEBM_AUDIO
    }

    enum StreamCodec
    {
        NONE,
        MP4A,
        VORBIS,
        OPUS,
        AVC,
        VP9,
        UNKNOWN
    }
}
