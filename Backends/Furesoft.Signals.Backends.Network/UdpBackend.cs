using System;

namespace Furesoft.Signals.Backends.Network
{
    public sealed class UdpBackend : ISignalBackend
    {
        public string Name { get; set; }

        public event Action<byte[]> OnNewMessage;

        public void Dispose()
        {
        }

        public void Initialize(string Name, long capacity, bool isOwner)
        {
            this.Name = Name;
        }

        public void Write(byte[] data)
        {
        }
    }
}