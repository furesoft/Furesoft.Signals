using Furesoft.Signals;
using System;

namespace TestModels
{
    public class PingArg : IpcMessage
    {
        public string Message { get; set; }
    }
}