using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Furesoft.Signals
{
    public class IpcChannel
    {
        internal MemoryMappedFileCommunicator communicator;
        internal MemoryMappedFileCommunicator event_communicator;
        internal MemoryMappedFileCommunicator func_communicator;

        internal Dictionary<int, MethodInfo> shared_functions = new Dictionary<int, MethodInfo>();

        public IpcChannel()
        {
            //ToDo: Fix
            shared_functions.Add((int)Signal.MethodConstants.GetSignature, GetMethodInfo(nameof(GetSignature)));
        }

        private MethodInfo GetMethodInfo(string name)
        {
            return GetType().GetMethod(name);
        }

        private Signature GetSignature(int id)
        {
            var sig = new Signature();

            var mi = shared_functions?[id];

            sig.ReturnType = mi.ReturnType.Name;
            sig.Description = (mi.GetCustomAttribute<DescriptionAttribute>() ?? new DescriptionAttribute()).Description;
            //sig.Parameters = BuildSigParameters(mi);

            return sig;
        }

        public static IpcChannel operator +(IpcChannel channel, Action<object> callback)
        {
            Signal.Subscribe(channel, callback);
            return channel;
        }

        public Action<IpcMessage> ToDelegate()
        {
            return new Action<IpcMessage>(msg =>
           {
               Signal.Send(this, msg);
           });
        }

        public Action<EventType> ToDelegate<EventType>()
        {
            return new Action<EventType>(msg =>
           {
               Signal.CallEvent(this, msg);
           });
        }
    }
}