using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Furesoft.Signals
{
    public sealed class IpcChannel : IDisposable
    {
        public IpcChannel()
        {
            shared_functions.Add((int)Signal.MethodConstants.GetSignature, GetMethodInfo(nameof(GetSignature)));
            shared_functions.Add((int)Signal.MethodConstants.GetAllIds, GetMethodInfo(nameof(GetMethodIds)));

            this.Retain();
        }

        ~IpcChannel()
        {
            this.Release();
        }

        public static IpcChannel operator +(IpcChannel channel, Action<object> callback)
        {
            Signal.Subscribe(channel, callback);
            return channel;
        }

        public void Dispose()
        {
            event_communicator.Dispose();
            func_communicator.Dispose();
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

        internal ISignalBackend event_communicator;
        internal ISignalBackend func_communicator;
        internal List<int> notTrackedfuncs = new List<int>();
        internal Dictionary<int, MethodInfo> shared_functions = new Dictionary<int, MethodInfo>();

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

        private int[] GetMethodIds()
        {
            return shared_functions.Keys.ToArray();
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
    }
}