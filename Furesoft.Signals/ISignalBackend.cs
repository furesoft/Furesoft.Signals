using System;

namespace Furesoft.Signals
{
    public interface ISignalBackend
    {
        event Action<byte[]> OnNewMessage;

        void Initialize(bool isOwner);

        void Write(byte[] data);
    }
}