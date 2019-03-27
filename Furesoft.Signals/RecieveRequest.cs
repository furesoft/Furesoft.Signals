using System;

namespace Furesoft.Signals
{
    internal class RecieveRequest
    {
        public Type Type { get; internal set; }
        public Action<object> Callback { get; internal set; }
        public string Name { get; internal set; }
        public bool IsEndless { get; internal set; }
    }
}