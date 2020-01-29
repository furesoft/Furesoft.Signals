namespace Furesoft.Signals
{
    public interface ISignalBackend
    {
        void OnNewMessage(byte[] data);

        void Write(byte[] data);
    }
}