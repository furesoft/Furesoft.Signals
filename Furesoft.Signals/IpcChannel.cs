using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Furesoft.Signals
{
    public class IpcChannel : IDisposable
    {
        internal MemoryMappedFileCommunicator communicator;
        internal MemoryMappedFileCommunicator event_communicator;
        internal MemoryMappedFileCommunicator func_communicator;

        internal Dictionary<int, MethodInfo> shared_functions = new Dictionary<int, MethodInfo>();
        internal List<int> notTrackedfuncs = new List<int>();

        public IpcChannel()
        {
            shared_functions.Add((int)Signal.MethodConstants.GetSignature, GetMethodInfo(nameof(GetSignature)));
            shared_functions.Add((int)Signal.MethodConstants.GetAllSignatures, GetMethodInfo(nameof(GetAllSignatures)));
        }

        private Signature[] GetAllSignatures()
        {
            var funcs = shared_functions.Where(_ => !notTrackedfuncs.Contains(_.Key));

            return funcs.Select(_ => GetSignature(_.Key)).ToArray();
        }

        private MethodInfo GetMethodInfo(string name)
        {
            return GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private Signature GetSignature(int id)
        {
            if (notTrackedfuncs.Contains(id)) return Signature.Empty;

            if (shared_functions.ContainsKey(id))
            {
                var sig = new Signature();
                var mi = shared_functions[id];

                sig.ReturnType = mi.ReturnType.Name;
                sig.ID = id;
                sig.Description = (mi.GetCustomAttribute<DescriptionAttribute>() ?? new DescriptionAttribute()).Description;
                sig.Parameters = BuildSigParameters(mi.GetParameters());

                return sig;
            }

            return Signature.Empty;
        }

        private SignatureParameter[] BuildSigParameters(ParameterInfo[] pi)
        {
            var res = new List<SignatureParameter>();

            foreach (var p in pi)
            {
                var sip = new SignatureParameter
                {
                    Name = p.Name,
                    Type = p.ParameterType.Name,
                    IsOptional = p.IsOptional,
                    Description = (p.GetCustomAttribute<DescriptionAttribute>() ?? new DescriptionAttribute()).Description
                };

                res.Add(sip);
            }

            return res.ToArray();
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

        public Func<TArg, TResult> ToFunc<TArg, TResult>(int id)
        {
            return new Func<TArg, TResult>(msg =>
            {
                return Signal.CallMethod<TResult>(this, id, msg);
            });
        }

        public Func<TArg, TArg2, TResult> ToFunc<TArg, TArg2, TResult>(int id)
        {
            return new Func<TArg, TArg2, TResult>((a1, a2) =>
             {
                 return Signal.CallMethod<TResult>(this, id, a1, a2);
             });
        }

        public Func<TArg, TArg2, TArg3, TResult> ToFunc<TArg, TArg2, TArg3, TResult>(int id)
        {
            return new Func<TArg, TArg2, TArg3, TResult>((a1, a2, a3) =>
            {
                return Signal.CallMethod<TResult>(this, id, a1, a2, a3);
            });
        }

        public void Dispose()
        {
            communicator.Dispose();
            event_communicator.Dispose();
            func_communicator.Dispose();
        }
    }
}