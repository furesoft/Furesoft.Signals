namespace Furesoft.Signals.Streaming
{
    internal class IpcStreamChunk
    {
        public int Length { get; set; }
        public int Position { get; set; }
        public byte[] Buffer { get; set; }
    }
}