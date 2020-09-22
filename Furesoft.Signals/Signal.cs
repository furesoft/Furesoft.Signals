using Furesoft.Signals.Attributes;
using Furesoft.Signals.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Furesoft.Signals
{
    public static class Signal
    {
        public static IpcChannel OpenChannel(string name)
        {
            try
            {
                MemoryMappedFile.OpenExisting(name);
                return CreateRecieverChannel(name);
            }
            catch (FileNotFoundException ex)
            {
                return CreateSenderChannel(name);
            }
        }

        public static ISerializer Serializer = new JsonSerializer();

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
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

            channel.func_communicator.OnNewMessage += (data) =>
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
                                tcs.TrySetResult((T)JsonConvert.DeserializeObject(Serializer.Deserialize<string>(resp.ReturnValue)));
                            }
                            else
                            {
                                tcs.TrySetResult(Serializer.Deserialize<T>(resp.ReturnValue));
                            }
                        }
                    }
                    else
                    {
                        var ex = new Exception(resp.ErrorMessage);

                        tcs.TrySetException(ex);
                    }
                }
            };

            var m = new FunctionCallRequest
            {
                ID = id,
                ParameterRaw = arg.Select(_ => Serializer.Serialize(_)).ToArray()
            };

            var raw = Serializer.Serialize(m);

            channel.func_communicator.Write(raw);

            return tcs.Task;
        }

        public static void CollectAllShared(IpcChannel channel)
        {
            var assembly = Assembly.GetCallingAssembly();

            CollectShared(channel, assembly.GetTypes());
        }

        public static void CollectShared(IpcChannel channel, params Type[] types)
        {
            Logger.Trace("Collecting Shared Functions");

            channel.func_communicator.OnNewMessage += (data) =>
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

                var id = methodInfo.GetCustomAttribute<SharedFunctionAttribute>()?.ID;

                try
                {
                    try
                    {
                        if (methodInfo.IsStatic)
                        {
                            res = methodInfo.Invoke(null, args);
                        }
                        else
                        {
                            res = methodInfo.Invoke(channel, args);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message.ToOptional();
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
                channel.func_communicator.Write(raw);
            };

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
                            if (!channel.shared_functions.ContainsKey(mattr.ID))
                            {
                                channel.shared_functions.Add(mattr.ID, m);
                            }
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
                event_communicator = new TBackend(),
                func_communicator = new TBackend()
            };

            if (NLog.LogManager.Configuration == null)
            {
                var config = new NLog.Config.LoggingConfiguration();
                var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                // Rules for mapping loggers to targets
                config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logconsole);

                // Apply config
                NLog.LogManager.Configuration = config;
            }

            channel.event_communicator.Initialize(name + ".events", 4096, true);
            channel.func_communicator.Initialize(name + ".funcs", 4096, true);

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

        private static IpcChannel CreateRecieverChannel<TBackend>(int name, Action<IpcConfiguration> configurator = null)
            where TBackend : ISignalBackend, new()
        {
            return CreateRecieverChannel<TBackend>(name.ToString(), configurator);
        }

        private static IpcChannel CreateRecieverChannel(string name, Action<IpcConfiguration> configurator = null)
        {
            return CreateRecieverChannel<MmfCommunicatorBackend>(name, configurator);
        }

        private static IpcChannel CreateSenderChannel(string name, Action<IpcConfiguration> configurator = null)
        {
            return CreateSenderChannel<MmfCommunicatorBackend>(name, configurator);
        }

        private static IpcChannel CreateSenderChannel<TBackend>(string name, Action<IpcConfiguration> configurator = null)
            where TBackend : ISignalBackend, new()
        {
            var channel = new IpcChannel
            {
                event_communicator = new TBackend(),
                func_communicator = new TBackend()
            };

            channel.event_communicator.Initialize(name + ".events", 4096, false);
            channel.func_communicator.Initialize(name + ".funcs", 4096, false);

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

        private static IpcChannel CreateSenderChannel(int name, Action<IpcConfiguration> configurator = null)
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

        public static void EnableLogging()
        {
            if (NLog.LogManager.Configuration == null)
            {
                var config = new NLog.Config.LoggingConfiguration();
                var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                // Rules for mapping loggers to targets
                config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logconsole);

                // Apply config
                NLog.LogManager.Configuration = config;
            }
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

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

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