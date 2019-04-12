using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Furesoft.Signals.Streaming
{
    public class IpcStream : Stream
    {
        private IpcChannel _com;
        private Queue<IpcStreamChunk> _chunks = new Queue<IpcStreamChunk>();
        private AutoResetEvent are = new AutoResetEvent(false);

        internal IpcStream(IpcChannel com)
        {
            _com = com;
            _com.stream_communicator.DataReceived += _com_DataReceived;
            _com.stream_communicator.StartReader();
        }

        private void _com_DataReceived(object sender, DataReceivedEventArgs e)
        {
            var chunk = Signal.Serializer.Deserialize<IpcStreamChunk>(e.Data);

            _chunks.Enqueue(chunk);
            are.Set();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new System.NotImplementedException();

        public override long Position { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override void Flush()
        {
            return;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            are.WaitOne();

            var chunk = _chunks.Dequeue();

            Array.Copy(chunk.Buffer, buffer, buffer.Length);

            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            //send IpcStreamSeekRequest
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            return;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var chunk = new IpcStreamChunk
            {
                Buffer = buffer.Skip(offset).Take(count).ToArray()
            };

            _com.stream_communicator.Write(Signal.Serializer.Serialize(chunk));
        }
    }
}