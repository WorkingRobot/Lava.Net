using CSCore;
using CSCore.Ffmpeg;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lava.Net.Streams
{
    class LavaStream : Stream
    {
        public LavaSampleToPcm16 Decoder;
        ISampleSource SampleSource;
        EqualizerStream Equalizer;
        public IWaveSource OldDecoder;
        public readonly Stream Stream;
        public bool Completed { get; private set; } = false;
        private long EndTime = -1;

        public LavaStream(Stream stream)
        {
            Stream = stream;
            try
            {
                OldDecoder = new FfmpegDecoder(stream).ChangeSampleRate(48000);
                SampleSource = OldDecoder.ToSampleSource().ToStereo();
                Equalizer = new EqualizerStream(SampleSource);
                Decoder = new LavaSampleToPcm16(Equalizer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
            }
        }

        public static async Task<LavaStream> FromUrl(string url) => new LavaStream(await Utils.GetStream(url));

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => Decoder.Length;

        public override long Position { get => Decoder.Position; set => Decoder.Position = value; }

        public override void Flush()
        {
            throw new InvalidOperationException("Cannot flush");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Completed) return 0;
            //Console.WriteLine("P"+OldDecoder.Position);
            //Console.WriteLine("L"+OldDecoder.GetLength());
            if ((OldDecoder.Length != 0 && OldDecoder.Position == OldDecoder.Length) || (EndTime != -1 && Decoder.Position >= EndTime))
            {
                Console.WriteLine("Completed!");
                Completed = true;
                return 0;
            }
            int read = Decoder.Read(buffer, offset, count);
            if (read == 0)
            {
                Console.WriteLine("Completed?");
                Completed = true;
            }
            //Console.WriteLine("R"+read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return Position = offset;
                case SeekOrigin.Current:
                    return Position += offset;
                case SeekOrigin.End:
                    return Position = Length - Position;
            }
            throw new ArgumentException("Not a valid value.", nameof(origin));
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Cannot write");
        }
        
        public override void Close()
        {
            Completed = true;
            Decoder.Dispose();
            Stream.Close();
        }

        public void SetEndTime(long endTime)
        {
            if (endTime != -1)
                EndTime = Decoder.WaveFormat.MillisecondsToBytes(endTime);
        }

        public void SetVolume(float volume)
        {
            Decoder.Volume = volume;
        }

        public void SetEqualizer(EqualizerBand[] bands)
        {
            foreach (var band in bands)
            {
                Equalizer.SetBandGain(band.Band, band.Gain);
            }
            Equalizer.Enabled = true;
        }
    }
}
