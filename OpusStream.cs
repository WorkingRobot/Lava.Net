using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lava.Net
{
    class OpusStream : Stream
    {
        Func<byte[], int, int, uint, ushort, Task> SendOpus;
        BufferedStream Buffered;
        
        public OpusStream(Func<byte[], int, int, uint, ushort, Task> sendOpus)
        {
            SendOpus = sendOpus;
            Buffered = new BufferedStream(sendOpus);
            Buffered.StartTask(CancellationToken.None);
        }
        
        private readonly byte[] _buffer = new byte[OpusEncoder.FRAME_BYTES];
        private int _partialFramePos;
        private ushort _seq;
        private uint _timestamp;

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            //Assume thread-safe
            while (count > 0)
            {
                if (_partialFramePos == 0 && count >= OpusEncoder.FRAME_BYTES)
                {
                    //We have enough data and no partial frames. Pass the buffer directly to the encoder
                    int encFrameSize = OpusEncoder.EncodeFrame(buffer, offset, _buffer, 0);
                    Buffered.RecvFrame(new Memory<byte>(_buffer, 0, encFrameSize).ToArray());

                    offset += OpusEncoder.FRAME_BYTES;
                    count -= OpusEncoder.FRAME_BYTES;
                    _seq++;
                    _timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                }
                else if (_partialFramePos + count >= OpusEncoder.FRAME_BYTES)
                {
                    //We have enough data to complete a previous partial frame.
                    int partialSize = OpusEncoder.FRAME_BYTES - _partialFramePos;
                    Buffer.BlockCopy(buffer, offset, _buffer, _partialFramePos, partialSize);
                    int encFrameSize = OpusEncoder.EncodeFrame(_buffer, 0, _buffer, 0);
                    Buffered.RecvFrame(new Memory<byte>(_buffer, 0, encFrameSize).ToArray());

                    offset += partialSize;
                    count -= partialSize;
                    _partialFramePos = 0;
                    _seq++;
                    _timestamp += OpusEncoder.FRAME_SAMPLES_PER_CHANNEL;
                }
                else
                {
                    //Not enough data to build a complete frame, store this part for later
                    Buffer.BlockCopy(buffer, offset, _buffer, _partialFramePos, count);
                    _partialFramePos += count;
                    break;
                }
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
