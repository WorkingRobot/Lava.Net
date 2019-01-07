using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class BufferedStream
    {
        const int MAX_SILENCE_FRAMES = 10;
        const int TICKS_PER_FRAME = OpusEncoder.FRAME_MILLIS;
        static readonly byte[] SILENCE_FRAME = new byte[0];
        
        Func<byte[], int, int, uint, ushort, Task> SendOpus;
        Task BufferTask;
        ConcurrentQueue<byte[]> frames = new ConcurrentQueue<byte[]>();

        public BufferedStream(Func<byte[], int, int, uint, ushort, Task> sendOpus)
        {
            SendOpus = sendOpus;
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
            BufferTask = StartBuffer(cancelToken);
        }

        private int _silenceFrames;
        async Task StartBuffer(CancellationToken _cancelToken)
        {
            try
            {
                long nextTick = Environment.TickCount;
                ushort seq = 0;
                uint timestamp = 0;
                while (!_cancelToken.IsCancellationRequested)
                {
                    long tick = Environment.TickCount;
                    long dist = nextTick - tick;
                    if (dist <= 0)
                    {
                        if (frames.TryDequeue(out byte[] frame))
                        {
                            //await _client.SetSpeakingAsync(true).ConfigureAwait(false);
                            await SendOpus(frame, 0, frame.Length, timestamp, seq).ConfigureAwait(false);
                            nextTick += TICKS_PER_FRAME;
                            seq++;
                            timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                            _silenceFrames = 0;
                            Console.WriteLine($"Sent {frame.Length} bytes ({frames.Count} frames buffered)");
                        }
                        else
                        {
                            while ((nextTick - tick) <= 0)
                            {
                                if (_silenceFrames++ < MAX_SILENCE_FRAMES)
                                {
                                    await SendOpus(SILENCE_FRAME, 0, SILENCE_FRAME.Length, timestamp, seq).ConfigureAwait(false);
                                }
                                else
                                {
                                    //await _client.SetSpeakingAsync(false).ConfigureAwait(false);
                                }
                                nextTick += TICKS_PER_FRAME;
                                seq++;
                                timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                            }
                            Console.WriteLine("Buffer underrun");
                        }
                    }
                    else
                        await Task.Delay((int)(dist)/*, _cancelToken*/).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
