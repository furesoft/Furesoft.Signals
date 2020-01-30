using Furesoft.Signals.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Furesoft.Signals
{
    public class SharedStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get; set; }

        public SharedStream(IpcChannel channel)
        {
            _channel = channel;
            _channel.stream_communicator.OnNewMessage += onNewMessage;
        }

        public override void Close()
        {
            _channel.Dispose();
        }

        public override void Flush()
        {
            foreach (var chunk in _writeBuffer)
            {
                var json = JsonConvert.SerializeObject(chunk);
                _channel.stream_communicator.Write(Encoding.ASCII.GetBytes(json));
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            mre.WaitOne();

            if (_readBuffer.Count > 0)
            {
                var data = _readBuffer.Dequeue()?.Data;
                if (data != null)
                {
                    Array.Copy(data, 0, buffer, 0, data.Length);
                }

                mre.Reset();

                return data.Length;
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

            _writeBuffer.Enqueue(chunk);
        }

        private IpcChannel _channel;
        private Queue<StreamChunk> _readBuffer = new Queue<StreamChunk>();
        private Queue<StreamChunk> _writeBuffer = new Queue<StreamChunk>();

        private int lastID = 0;
        private ManualResetEvent mre = new ManualResetEvent(false);

        private void onNewMessage(byte[] data)
        {
            var rawString = System.Text.Encoding.UTF8.GetString(data);
            var desObj = JsonConvert.DeserializeObject<StreamChunk>(rawString);

            if (desObj != null)
            {
                _readBuffer.Enqueue(desObj);
            }
            mre.Set();
        }
    }
}