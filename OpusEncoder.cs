using System;
using System.Runtime.InteropServices;

namespace Lava.Net
{
    static unsafe class OpusEncoder
    {
        const int SAMPLING_RATE = 48000;
        const int CHANNELS = 2;
        internal const int FRAME_MILLIS = 20;
        const int SAMPLE_BYTES = sizeof(short) * CHANNELS;
        const int BITRATE = 128*1024;
        internal const int FRAME_SAMPLES_PER_CHANNEL = SAMPLING_RATE / 1000 * FRAME_MILLIS;
        internal const int FRAME_BYTES = FRAME_SAMPLES_PER_CHANNEL * SAMPLE_BYTES;


        const OpusApplication OPUS_APPLICATION = OpusApplication.MusicOrMixed;
        const OpusSignal OPUS_SIGNAL = OpusSignal.Music;

        static IntPtr _ptr;

        static OpusEncoder()
        {
            if (BITRATE < 1 || BITRATE > 128 * 1024)
                throw new ArgumentOutOfRangeException(nameof(BITRATE));

            _ptr = CreateEncoder(SAMPLING_RATE, CHANNELS, (int)OPUS_APPLICATION, out var error);
            CheckError(error);
            CheckError(EncoderCtl(_ptr, OpusControl.SetSignal, (int)OPUS_SIGNAL));
            CheckError(EncoderCtl(_ptr, OpusControl.SetPacketLossPercent, 15)); //%
            CheckError(EncoderCtl(_ptr, OpusControl.SetInbandFEC, 1)); //True
            CheckError(EncoderCtl(_ptr, OpusControl.SetBitrate, BITRATE));
        }

        public static unsafe int EncodeFrame(byte[] input, int inputOffset, byte[] output, int outputOffset)
        {
            int result = 0;
            fixed (byte* inPtr = input)
            fixed (byte* outPtr = output)
                result = Encode(_ptr, inPtr + inputOffset, FRAME_SAMPLES_PER_CHANNEL, outPtr + outputOffset, output.Length - outputOffset);
            CheckError(result);
            return result;
        }

        /*
        public static unsafe byte[] Encode(byte[] frame, int bitRate = 16)
        {
            var frame_size = FrameCount(frame.Length, bitRate);
            var encdata = IntPtr.Zero;
            var enc = new byte[frame.Length];
            int len = 0;

            fixed (byte* encptr = enc)
            {
                encdata = new IntPtr(encptr);
                len = Encode(_ptr, frame, frame_size, encdata, enc.Length);
            }
            if (len < 0)
            {
                CheckError((OpusError)len);
            }

            Array.Resize(ref enc, len);
            return enc;
        }

        static int FrameCount(int length, int bitRate)
        {
            int bps = (bitRate >> 2) & ~1; // (bitrate / 8) * 2;
            return length / bps;
        }*/

        static void CheckError(OpusError error) => CheckError((int)error);

        static void CheckError(int error)
        {
            if (error < 0)
            {
                throw new Exception("Opus Error: " + ((OpusError)error).ToString());
            }
        }

        [DllImport("opus", EntryPoint = "opus_encoder_create", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateEncoder(int Fs, int channels, int application, out OpusError error);
        [DllImport("opus", EntryPoint = "opus_encoder_destroy", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyEncoder(IntPtr encoder);
        [DllImport("opus", EntryPoint = "opus_encode", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Encode(IntPtr st, byte* pcm, int frame_size, byte* data, int max_data_bytes);
        [DllImport("opus", EntryPoint = "opus_encoder_ctl", CallingConvention = CallingConvention.Cdecl)]
        private static extern OpusError EncoderCtl(IntPtr st, OpusControl request, int value);
    }
    
    internal enum OpusError : int
    {
        Ok = 0,
        BadArgument = -1,
        BufferTooSmall = -2,
        InternalError = -3,
        InvalidPacket = -4,
        Unimplemented = -5,
        InvalidState = -6,
        AllocationFailure = -7
    }

    internal enum OpusControl : int
    {
        SetBitrate = 4002,
        SetBandwidth = 4008,
        SetInbandFEC = 4012,
        SetPacketLossPercent = 4014,
        SetSignal = 4024,
        ResetState = 4028
    }

    internal enum OpusSignal : int
    {
        Auto = -1000,
        Voice = 3001,
        Music = 3002,
    }

    internal enum OpusApplication : int
    {
        Voice = 2048,
        MusicOrMixed = 2049,
        LowLatency = 2051
    }
}
