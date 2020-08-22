namespace Furesoft.Signals.Messages
{
    internal class QueueMessage
    {
        public string Typename { get; set; }
        public object Argument { get; set; }
    }
}