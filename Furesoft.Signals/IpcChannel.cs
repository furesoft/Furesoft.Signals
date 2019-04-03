using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Furesoft.Signals
{
    public class IpcChannel
    {
        internal MemoryMappedFileCommunicator communicator;
        internal MemoryMappedFileCommunicator event_communicator;

        internal Dictionary<int, MethodInfo> shared_functions = new Dictionary<int, MethodInfo>();

        public static IpcChannel operator +(IpcChannel channel, Action<object> callback)
        {
            Signal.Subscribe(channel, callback);
            return channel;
        }

        public Action<IpcMessage> ToDelegate()
        {
            return new Action<IpcMessage>( msg =>
            {
                Signal.Send(this, msg);
            });
        }

        public Action<EventType> ToDelegate<EventType>()
        {
            return new Action<EventType>( msg =>
            {
                Signal.CallEvent(this, msg);
            });
        }

    }
}