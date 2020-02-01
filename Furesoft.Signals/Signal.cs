using Furesoft.Signals.Attributes;
using Furesoft.Signals.Backends;
using Furesoft.Signals.Core;
using Furesoft.Signals.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Furesoft.Signals
{
    public static class Signal
    {
        public static ISerializer Serializer = new Serializers.JsonSerializer();

        public static void CallEvent<EventType>(IpcChannel channel, EventType et)
        {
            var objid = typeof(EventType).GUID;
            var serialized = Serializer.Serialize(et);

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write(objid.ToByteArray());
            bw.Write(serialized);

            var raw = ms.ToArray();

            channel.event_communicator.Write(raw);
        }

        public static T CallMethod<T>(IpcChannel channel, int id, params object[] arg)
        {
            return Task.Run(() => CallMethodAsync<T>(channel, id, arg)).Result;
        }

        public static Task<T> CallMethodAsync<T>(IpcChannel channel, int id, params object[] arg)
        {
            mre.Reset();
            T ret = default;
            string json = null;
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

            channel.communicator.OnNewMessage += (data) =>
            {
                var resp = Serializer.Deserialize<FunctionCallResponse>(data);

                if (resp.ID == id)
                {
                    if (string.IsNullOrEmpty(resp.ErrorMessage))
                    {
                        if (resp.ReturnValue != null)
                        {
                            if (typeof(T) == typeof(JObject))
                            {
                                json = Serializer.Deserialize<string>(resp.ReturnValue);
                            }
                            else
                            {
                                ret = Serializer.Deserialize<T>(resp.ReturnValue);
                            }
                        }
                    }
                    else
                    {
                        var ex = new Exception(resp.ErrorMessage);
                        MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (preserveStackTrace != null) preserveStackTrace.Invoke(ex, null);

                        tcs.TrySetException(ex);
                    }

                    mre.Set();
                }
            };

            var m = new FunctionCallRequest
            {
                ID = id,
                ParameterRaw = arg.Select(_ => Serializer.Serialize(_)).ToArray()
            };

            var raw = Serializer.Serialize(m);

            channel.communicator.Write(raw);

            mre.WaitOne();

            if (typeof(T) == typeof(JObject))
            {
                tcs.TrySetResult((T)JsonConvert.DeserializeObject(json));
            }
            else
            {
                tcs.TrySetResult(ret);
            }

            return tcs.Task;
        }

        public static void CollectAllShared(IpcChannel channel)
        {
            var assembly = Assembly.GetCallingAssembly();

            CollectShared(channel, assembly.GetTypes());
        }

        public static void CollectShared(IpcChannel channel, params Type[] types)
        {
            foreach (var t in types)
            {
                var attr = t.GetCustomAttribute<SharedAttribute>();

                if (attr != null)
                {
                    foreach (var m in t.GetMethods())
                    {
                        var mattr = m.GetCustomAttribute<SharedFunctionAttribute>();

                        if (mattr != null)
                        {
                            if (m.GetCustomAttribute<NoSignatureAttribute>() != null)
                            {
                                channel.notTrackedfuncs.Add(mattr.ID);
                            }

                            if (!channel.shared_functions.ContainsKey(mattr.ID))
                            {
                                channel.shared_functions.Add(mattr.ID, m);
                            }

                            channel.communicator.OnNewMessage += (data) =>
                             {
                                 var obj = Serializer.Deserialize<FunctionCallRequest>(data);

                                 Optional<string> error = false;
                                 object res = null;
                                 object[] args = null;

                                 var methodInfo = channel.shared_functions[obj.ID];
                                 var parameterInfo = methodInfo.GetParameters();

                                 if (!IsArgumentMismatch(parameterInfo, obj.ParameterRaw))
                                 {
                                     args = GetDeserializedParameters(parameterInfo, obj.ParameterRaw);
                                 }

                                 var tm = (IFuncFilter)methodInfo.GetCustomAttributes(true).Where(x => x is IFuncFilter).FirstOrDefault();
                                 var filterAtt = tm ?? new DefaultFuncFilter();
                                 var id = methodInfo.GetCustomAttribute<SharedFunctionAttribute>()?.ID;

                                 try
                                 {
                                     FuncFilterResult beforecallresult = filterAtt.BeforeCall(methodInfo, id ?? -1);
                                     if (beforecallresult)
                                     {
                                         if (methodInfo.IsStatic)
                                         {
                                             res = methodInfo.Invoke(null, args);
                                         }
                                         else
                                         {
                                             res = methodInfo.Invoke(channel, args);
                                         }

                                         res = filterAtt.AfterCall(methodInfo, id ?? -1, res);
                                     }
                                     else
                                     {
                                         if (beforecallresult.ErrorMessage)
                                         {
                                             error = beforecallresult.ErrorMessage;
                                         }
                                     }
                                 }
                                 catch (Exception ex)
                                 {
                                     error = ex.Message.ToOptional();
                                 }

                                 var resp = new FunctionCallResponse
                                 {
                                     ID = obj.ID
                                 };

                                 if (!error)
                                 {
                                     resp.ReturnValue = Serializer.Serialize(res);
                                 }
                                 else
                                 {
                                     resp.ErrorMessage = error;
                                 }

                                 var raw = Serializer.Serialize(resp);
                                 channel.communicator.Write(raw);
                             };
                        }
                    }
                }
            }
        }

        public static IpcChannel CreateRecieverChannel<TBackend>(string name, Action<IpcConfiguration> configurator = null)
            where TBackend : ISignalBackend, new()
        {
            var channel = new IpcChannel
            {
                communicator = new TBackend(),
                event_communicator = new TBackend(),
                func_communicator = new TBackend(),
                stream_communicator = new TBackend()
            };

            channel.communicator.Initialize(name, 4096, true);
            channel.event_communicator.Initialize(name + ".events", 4096, true);
            channel.func_communicator.Initialize(name + ".funcs", 4096, true);
            channel.stream_communicator.Initialize(name + ".chunks", 4096, true);

            if (configurator != null)
            {
                var config = new IpcConfiguration();
                configurator(config);

                foreach (var func in config.shared_functions)
                {
                    channel.shared_functions.Add(func.Key, func.Value);
                }
            }

            return channel;
        }

        public static IpcChannel CreateRecieverChannel<TBackend>(int name, Action<IpcConfiguration> configurator = null)
            where TBackend : ISignalBackend, new()
        {
            return CreateRecieverChannel<TBackend>(name.ToString(), configurator);
        }

        public static IpcChannel CreateRecieverChannel(string name, Action<IpcConfiguration> configurator = null)
        {
            return CreateRecieverChannel<MmfCommunicatorBackend>(name, configurator);
        }

        public static IpcChannel CreateSenderChannel(string name, Action<IpcConfiguration> configurator = null)
        {
            return CreateSenderChannel<MmfCommunicatorBackend>(name, configurator);
        }

        public static IpcChannel CreateSenderChannel<TBackend>(string name, Action<IpcConfiguration> configurator = null)
            where TBackend : ISignalBackend, new()
        {
            var channel = new IpcChannel
            {
                communicator = new TBackend(),
                event_communicator = new TBackend(),
                func_communicator = new TBackend(),
                stream_communicator = new TBackend()
            };

            channel.communicator.Initialize(name, 4096, false);
            channel.event_communicator.Initialize(name + ".events", 4096, false);
            channel.func_communicator.Initialize(name + ".funcs", 4096, false);
            channel.stream_communicator.Initialize(name + ".chunks", 4096, false);

            if (configurator != null)
            {
                var config = new IpcConfiguration();
                configurator(config);

                foreach (var func in config.shared_functions)
                {
                    channel.shared_functions.Add(func.Key, func.Value);
                }
            }

            return channel;
        }

        public static IpcChannel CreateSenderChannel(int name, Action<IpcConfiguration> configurator = null)
        {
            return CreateSenderChannel(name.ToString(), configurator);
        }

        public static SharedObject<T> CreateSharedObject<T>(int id, bool sender = false)
        {
            if (sender)
            {
                return SharedObject<T>.CreateSender(id);
            }

            return SharedObject<T>.CreateReciever(id);
        }

        public static Stream CreateSharedStream(IpcChannel channel)
        {
            var sStrm = new SharedStream(channel);

            return sStrm;
        }

        public static Signature GetSignatureOf(IpcChannel channel, int id)
        {
            return CallMethod<Signature>(channel, (int)MethodConstants.GetSignature, id);
        }

        public static void Recieve<T>(Action<T> callback)
        {
            recieveQueue.Enqueue(
                new RecieveRequest
                {
                    Type = typeof(T),
                    Callback = new Action<object>(
                        o => callback((T)o)),
                    Name = typeof(T).Name
                });
        }

        public static void Send(IpcChannel channel, IpcMessage msg)
        {
            var raw = Serializer.Serialize(msg);

            channel.communicator.Write(raw);
        }

        public static void Subscribe<EventType>(IpcChannel channel, Action<EventType> callback)
        {
            channel.event_communicator.OnNewMessage += (data) =>
            {
                var objid = typeof(EventType).GUID;

                if (objid == new Guid(data.Take(16).ToArray()))
                {
                    var obj = Serializer.Deserialize<EventType>(data.Skip(16).ToArray());

                    callback(obj);
                }
            };
        }

        internal enum MethodConstants
        {
            GetSignature = 316497852,
            GetAllIds = 316497853,
        }

        private static readonly ManualResetEvent mre = new ManualResetEvent(false);
        private static readonly Queue<RecieveRequest> recieveQueue = new Queue<RecieveRequest>();

        private static void Communicator_DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (recieveQueue.Count > 0)
            {
                var request = recieveQueue.Dequeue();
                var obj = Serializer.Deserialize(e.Data, request.Type);

                request.Callback(obj);
            }
        }

        private static object[] GetDeserializedParameters(ParameterInfo[] parameterInfo, byte[][] parameterRaw)
        {
            var res = new List<object>();
            for (int i = 0; i < parameterRaw.Length; i++)
            {
                res.Add(Serializer.Deserialize(parameterRaw[i], parameterInfo[i].ParameterType));
            }
            return res.ToArray();
        }

        private static bool IsArgumentMismatch(ParameterInfo[] parameterInfo, byte[][] parameterJson)
        {
            return parameterInfo.Length != parameterJson.Length;
        }
    }
}