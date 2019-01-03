using System;

namespace Lava.Net
{
    struct LavaStream
    {
        public Uri Uri;
        public StreamType Type;

        public LavaStream(Uri uri, StreamType type)
        {
            Uri = uri;
            Type = type;
        }
    }
}
