using Furesoft.Signals.Pipe;
using System;

namespace Furesoft.Signals.Backends
{
    public class MmfPipeBackend : ISignalBackend
    {
        public event Action<byte[]> OnNewMessage;

        public void Dispose()
        {
            _in.Dispose();
            _out.Dispose();
        }

        public void Initialize(string Name, long capacity, bool isOwner)
        {
            _in = new InPipe(Name, isOwner, OnNewMessage);
            _out = new OutPipe(Name, isOwner);
        }

        public void Write(byte[] data)
        {
            _out.Write(data);
        }

        private InPipe _in;
        private OutPipe _out;
    }
}