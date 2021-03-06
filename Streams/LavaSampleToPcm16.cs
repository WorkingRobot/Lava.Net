﻿using CSCore;
using CSCore.Streams.SampleConverter;
using System;

namespace Lava.Net.Streams
{
    /// <summary>
    /// Identical to <see cref="SampleToPcm16"/>, but the volume is slightly lowered due to the amount of peaking.
    /// </summary>
    public class LavaSampleToPcm16 : SampleToPcm16
    {
        public LavaSampleToPcm16(ISampleSource source) : base(source) { }

        float volume = 0.79f;
        public float Volume {
            get => volume / 0.79f;
            set => volume = value * 0.79f;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Buffer = Buffer.CheckBuffer(count / 2);

            int read = Source.Read(Buffer, 0, count / 2);
            int bufferOffset = offset;
            for (int i = 0; i < read; i++)
            {
                int value = (int)(Buffer[i] * volume * short.MaxValue); // Without modifier song turns to earrape :)
                var bytes = BitConverter.GetBytes(value);

                buffer[bufferOffset++] = bytes[0];
                buffer[bufferOffset++] = bytes[1];
            }

            return read * 2;
        }
    }
}
