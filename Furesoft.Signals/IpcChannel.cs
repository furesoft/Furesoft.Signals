using Furesoft.Signals.Core;
using System;

namespace Furesoft.Signals
{
    public class IpcChannel
    {
        internal MemoryMappedFileCommunicator communicator;
        internal MemoryMappedFileCommunicator event_communicator;

        public static IpcChannel operator +(IpcChannel channel, Action<object> callback)
        {
            Signal.Subscribe(channel, callback);
            return channel;
        }
    }
}