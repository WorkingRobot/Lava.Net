﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class BufferedStream
    {
        const int MAX_SILENCE_FRAMES = 10;
        const int TICKS_PER_FRAME = OpusEncoder.FRAME_MILLIS;
        static readonly byte[] SILENCE_FRAME = new byte[] { 0xF8, 0xFF, 0xFE };

        readonly Func<byte[], int, int, uint, ushort, Task> SendOpusAsync;
        readonly Func<Task> RequestFramesAsync;
        readonly Func<bool, Task> SetSpeakingAsync;
        readonly Action<FrameType> SentFrame;
        CancellationToken cancelToken;
        Task BufferTask;
        ConcurrentQueue<byte[]> frames = new ConcurrentQueue<byte[]>();
        bool Paused;

        public BufferedStream(Func<byte[], int, int, uint, ushort, Task> sendOpus, Func<Task> requestFrames, Func<bool, Task> setSpeaking, Action<FrameType> sentFrame)
        {
            SendOpusAsync = sendOpus;
            RequestFramesAsync = requestFrames;
            SetSpeakingAsync = setSpeaking;
            SentFrame = sentFrame;
        }

        public void RecvFrame(byte[] frame)
        {
            frames.Enqueue(frame);
        }

        public void StartTask(CancellationToken cancelToken)
        {
            if (BufferTask != null)
            {
                throw new NotSupportedException("You can't have a buffered stream start twice!");
            }
            this.cancelToken = cancelToken;
            BufferTask = Task.Factory.StartNew(
                function: StartBuffer,
                cancellationToken: cancelToken,
                creationOptions: TaskCreationOptions.LongRunning,
                scheduler: TaskScheduler.Default);
        }

        private int _silenceFrames;
        async Task StartBuffer()
        {
            try
            {
                long nextTick = Environment.TickCount;
                ushort seq = 0;
                uint timestamp = 0;
                while (Paused || !cancelToken.IsCancellationRequested || !frames.IsEmpty)
                {
                    long tick = Environment.TickCount;
                    long dist = nextTick - tick;
                    if (Paused)
                    {
                        nextTick += TICKS_PER_FRAME;
                        await Task.Delay(dist <= 0 ? TICKS_PER_FRAME : (int)dist, cancelToken).ConfigureAwait(false);
                        continue;
                    }
                    if (dist <= 0)
                    {
                        if (frames.TryDequeue(out byte[] frame))
                        {
                            await SetSpeakingAsync(true).ConfigureAwait(false);
                            await SendOpusAsync(frame, 0, frame.Length, timestamp, seq).ConfigureAwait(false);
                            SentFrame(FrameType.FULL);
                            nextTick += TICKS_PER_FRAME;
                            seq++;
                            timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                            _silenceFrames = 0;
                            //Console.WriteLine($"Sent {frame.Length} bytes ({frames.Count} frames buffered)");
                        }
                        else
                        {
                            while ((nextTick - tick) <= 0)
                            {
                                if (_silenceFrames++ < MAX_SILENCE_FRAMES)
                                {
                                    await SendOpusAsync(SILENCE_FRAME, 0, SILENCE_FRAME.Length, timestamp, seq).ConfigureAwait(false);
                                    SentFrame(FrameType.NULL);
                                }
                                else
                                {
                                    await SetSpeakingAsync(false).ConfigureAwait(false);
                                }
                                nextTick += TICKS_PER_FRAME;
                                seq++;
                                timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                            }
                        }
                        int _retries = 0;
                        while (frames.Count < 5 && _retries++ < 10)
                        {
                            await RequestFramesAsync().ConfigureAwait(false);
                        }
                    }
                    else
                        await Task.Delay((int)dist, cancelToken).ConfigureAwait(false);
                }
                await SendOpusAsync(SILENCE_FRAME, 0, SILENCE_FRAME.Length, timestamp, seq).ConfigureAwait(false);
                SentFrame(FrameType.NULL);
            }
            catch (OperationCanceledException) { }
        }

        public void Pause(bool paused)
        {
            Paused = paused;
        }
    }
}
