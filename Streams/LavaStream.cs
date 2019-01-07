using CSCore;
using CSCore.Ffmpeg;
using System;
using System.IO;

namespace Lava.Net.Streams
{
    class LavaStream : Stream
    {
        public IWaveSource Decoder;
        public LavaStream(Stream stream)
        {
            try
            {
                Decoder = new FfmpegDecoder(stream);
                Decoder = Decoder.ToSampleSource().ChangeSampleRate(48000).ToWaveSource(16);

                using (var f = File.OpenWrite(@"C:\Users\Aleks\Desktop\lol.wav"))
                {
                    Decoder.WriteToWaveStream(f);
                }
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
