using Furesoft.Signals;

namespace TestModels
{
    public class PingArg : IpcMessage
    {
        public string Message { get; set; }
    }
}