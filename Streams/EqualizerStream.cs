using CSCore;
using Newtonsoft.Json;
using System;

namespace Lava.Net.Streams
{
    class EqualizerStream : ISampleSource
    {
        ISampleSource Source;
        float[] BandMultipliers;

        public bool Enabled { get; set; } = false;

        public EqualizerStream(ISampleSource source, float[] bandMultipliers = default)
        {
            Source = source;
            BandMultipliers = bandMultipliers ?? new float[BAND_COUNT];
            processor = new ChannelProcessor(BandMultipliers);
        }

        public WaveFormat WaveFormat => Source.WaveFormat;

        public bool CanSeek => false;

        public long Position { get => Source.Position; set => Source.Position = value; }

        public long Length => Source.Length;

        public int Read(float[] buffer, int offset, int count)
        {
            int ret = Source.Read(buffer, offset, count);
            if (Enabled)
                Process(buffer, offset, count);
            return ret;
        }

        public void Dispose()
        {
            Source.Dispose();
        }

        public const int BAND_COUNT = 15;

        private const int SAMPLE_RATE = 48000;

        private static readonly Coefficients[] coefficients48000 = {
            new Coefficients(9.9847546664e-01f, 7.6226668143e-04f, 1.9984647656e+00f),
            new Coefficients(9.9756184654e-01f, 1.2190767289e-03f, 1.9975344645e+00f),
            new Coefficients(9.9616261379e-01f, 1.9186931041e-03f, 1.9960947369e+00f),
            new Coefficients(9.9391578543e-01f, 3.0421072865e-03f, 1.9937449618e+00f),
            new Coefficients(9.9028307215e-01f, 4.8584639242e-03f, 1.9898465702e+00f),
            new Coefficients(9.8485897264e-01f, 7.5705136795e-03f, 1.9837962543e+00f),
            new Coefficients(9.7588512657e-01f, 1.2057436715e-02f, 1.9731772447e+00f),
            new Coefficients(9.6228521814e-01f, 1.8857390928e-02f, 1.9556164694e+00f),
            new Coefficients(9.4080933132e-01f, 2.9595334338e-02f, 1.9242054384e+00f),
            new Coefficients(9.0702059196e-01f, 4.6489704022e-02f, 1.8653476166e+00f),
            new Coefficients(8.5868004289e-01f, 7.0659978553e-02f, 1.7600401337e+00f),
            new Coefficients(7.8409610788e-01f, 1.0795194606e-01f, 1.5450725522e+00f),
            new Coefficients(6.8332861002e-01f, 1.5833569499e-01f, 1.1426447155e+00f),
            new Coefficients(5.5267518228e-01f, 2.2366240886e-01f, 4.0186190803e-01f),
            new Coefficients(4.1811888447e-01f, 2.9094055777e-01f, -7.0905944223e-01f)
        };

        public void SetBandGain(byte band, float value)
        {
            if (band >= 0 && band <= BAND_COUNT)
            {
                BandMultipliers[band] = value;
            }
        }

        void Process(float[] input, int offset, int count)
        {
            processor.Process(input, offset, count);
        }

        readonly ChannelProcessor processor;

        private class ChannelProcessor
        {
            private float[] history;
            private float[] bandMultipliers;

            private int current;
            private int minusOne;
            private int minusTwo;

            public ChannelProcessor(float[] bandMultipliers)
            {
                history = new float[BAND_COUNT * 6];
                this.bandMultipliers = bandMultipliers;
                current = 0;
                minusOne = 2;
                minusTwo = 1;
            }

            internal void Process(float[] buffer, int offset, int count)
            {
                for (int sampleIndex = offset; sampleIndex < offset + count; sampleIndex++)
                {
                    float sample = buffer[sampleIndex];
                    float result = sample * 0.25f;

                    for (int bandIndex = 0; bandIndex < BAND_COUNT; bandIndex++)
                    {
                        int x = bandIndex * 6;
                        int y = x + 3;

                        Coefficients coefficients = coefficients48000[bandIndex];

                        float bandResult = coefficients.alpha * (sample - history[x + minusTwo]) +
                            coefficients.gamma * history[y + minusOne] -
                            coefficients.beta * history[y + minusTwo];

                        history[x + current] = sample;
                        history[y + current] = bandResult;

                        result += bandResult * bandMultipliers[bandIndex];
                    }

                    buffer[sampleIndex] = Math.Min(Math.Max(result * 4.0f, -1.0f), 1.0f);

                    if (++current == 3)
                    {
                        current = 0;
                    }

                    if (++minusOne == 3)
                    {
                        minusOne = 0;
                    }

                    if (++minusTwo == 3)
                    {
                        minusTwo = 0;
                    }
                }
            }

            void Reset()
            {
                Array.Clear(history, 0, history.Length);
            }
        }

        private sealed class Coefficients
        {
            internal readonly float beta;
            internal readonly float alpha;
            internal readonly float gamma;

            internal Coefficients(float beta, float alpha, float gamma)
            {
                this.beta = beta;
                this.alpha = alpha;
                this.gamma = gamma;
            }
        }
    }

    public struct EqualizerBand
    {
        [JsonProperty("band")]
        public byte Band;

        [JsonProperty("gain")]
        public float Gain;
    }
}
