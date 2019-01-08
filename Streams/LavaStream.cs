using CSCore;
using CSCore.Ffmpeg;
using System;
using System.IO;

namespace Lava.Net.Streams
{
    class LavaStream : Stream
    {
        public IWaveSource Decoder;
        public IWaveSource OldDecoder;
        public LavaStream(Stream stream)
        {
            try
            {
                OldDecoder = Decoder = new FfmpegDecoder(stream);
                Console.WriteLine($"Original: {Decoder.WaveFormat.SampleRate} {Decoder.WaveFormat.BitsPerSample}");
                Decoder = new LavaSampleToPcm16(Decoder.ChangeSampleRate(48000).ToSampleSource());
                Console.WriteLine(Decoder.WaveFormat.WaveFormatTag);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.ToString());
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => Decoder.Length;

        public override long Position { get => Decoder.Position; set => throw new InvalidOperationException("Cannot flush"); }

        public override void Flush()
        {
            throw new InvalidOperationException("Cannot flush");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Decoder.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Cannot seek");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Cannot write");
        }
    }
}
