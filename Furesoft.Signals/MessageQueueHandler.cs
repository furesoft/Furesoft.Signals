using System;

namespace Furesoft.Signals
{
    internal class MessageQueueHandler
    {
        public string Typename { get; set; }
        public Type Type { get; set; }
        public Delegate Action { get; set; }
    }
}