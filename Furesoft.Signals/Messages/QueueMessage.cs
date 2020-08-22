namespace Furesoft.Signals.Messages
{
    internal class QueueMessage
    {
        public string Typename { get; set; }
        public int TypeID { get; set; }
        public byte[] Raw { get; set; }
    }
}