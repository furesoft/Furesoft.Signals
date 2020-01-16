using Furesoft.Signals.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Furesoft.Signals
{
    public class SharedStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => lastChunk.Length;
        public override long Position { get; set; }

        public SharedStream(IpcChannel channel)
        {
            _channel = channel;
            _channel.stream_communicator.DataReceived += Stream_communicator_DataReceived;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            mre.WaitOne();

            if (_chunks.Any())
            {
                var data = _chunks.Dequeue().Data;
                Array.Copy(data, 0, buffer, 0, data.Length);
                mre.Reset();
            }

            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var chunk = new StreamChunk();
            chunk.ID = lastID++;
            chunk.Data = buffer;
            chunk.Length = buffer.Length;

            _channel.stream_communicator.Write(chunk.Serialize());
        }

        private IpcChannel _channel;
        private Queue<StreamChunk> _chunks = new Queue<StreamChunk>();
        private StreamChunk lastChunk;
        private int lastID = 0;
        private ManualResetEvent mre = new ManualResetEvent(false);

        private void Stream_communicator_DataReceived(object sender, Core.DataReceivedEventArgs e)
        {
            lastChunk = StreamChunk.Deserialize(e.Data);
            _chunks.Enqueue(lastChunk);
            mre.Set();
        }
    }
}