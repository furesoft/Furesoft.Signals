using Furesoft.Signals.Core;
using Furesoft.Signals.Messages;
using System;
using System.Collections.Generic;

namespace Furesoft.Signals
{
    internal class MessageQueueHandler
    {
        public string Typename { get; set; }
        public Delegate Action { get; set; }
    }
}