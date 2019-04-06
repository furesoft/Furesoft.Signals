using Furesoft.Signals.Attributes;
using Furesoft.Signals.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Furesoft.Signals
{
    public static class Signal
    {
        private static Queue<RecieveRequest> recieveQueue = new Queue<RecieveRequest>();

        public static void Subscribe<EventType>(IpcChannel channel, Action<EventType> callback)
        {
            channel.event_communicator.DataReceived += (s, e) =>
            {
                var objid = typeof(EventType).GUID;

                if (objid == new Guid(e.Data.Take(16).ToArray()))
                {
                    var obj = JsonConvert.DeserializeObject<EventType>(Encoding.ASCII.GetString(e.Data.Skip(16).ToArray()));

                    callback(obj);
                }
            };
        }

        public static IpcChannel CreateSenderChannel(string name)
        {
            var channel = new IpcChannel();

            channel.communicator = new MemoryMappedFileCommunicator(name, 4096);
            channel.communicator.ReadPosition = 2000;
            channel.communicator.WritePosition = 0;
            channel.communicator.DataReceived += new EventHandler<DataReceivedEventArgs>(Communicator_DataReceived);

            channel.communicator.StartReader();

            channel.event_communicator = new MemoryMappedFileCommunicator(name + ".events", 4096);
            channel.event_communicator.ReadPosition = 2000;
            channel.event_communicator.WritePosition = 0;
            channel.event_communicator.StartReader();

            channel.func_communicator = new MemoryMappedFileCommunicator(name + ".funcs", 4096);
            channel.func_communicator.ReadPosition = 2000;
            channel.func_communicator.WritePosition = 0;
            channel.func_communicator.StartReader();

            return channel;
        }

        public static IpcChannel CreateSenderChannel(int name)
        {
            return CreateSenderChannel(name.ToString());
        }

        public static IpcChannel CreateRecieverChannel(string name)
        {
            var channel = new IpcChannel();

            //Initialize Main communicator
            channel.communicator = new MemoryMappedFileCommunicator(name, 4096);
            channel.communicator.WritePosition = 2000;
            channel.communicator.ReadPosition = 0;
            channel.communicator.DataReceived += new EventHandler<DataReceivedEventArgs>(Communicator_DataReceived);
            channel.communicator.StartReader();

            //initialize event communicator
            channel.event_communicator = new MemoryMappedFileCommunicator(name + ".events", 4096);
            channel.event_communicator.WritePosition = 2000;
            channel.event_communicator.ReadPosition = 0;
            channel.event_communicator.StartReader();

            //initialize func communicator
            channel.func_communicator = new MemoryMappedFileCommunicator(name + ".funcs", 4096);
            channel.func_communicator.WritePosition = 2000;
            channel.func_communicator.ReadPosition = 0;
            channel.func_communicator.StartReader();

            return channel;
        }

        private static ManualResetEvent mre = new ManualResetEvent(false);

        public static IpcChannel CreateRecieverChannel(int name)
        {
            return CreateRecieverChannel(name.ToString());
        }

        private static void Communicator_DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (recieveQueue.Count > 0)
            {
                var request = recieveQueue.Dequeue();
                var obj = JsonConvert.DeserializeObject(Encoding.ASCII.GetString(e.Data), request.Type);

                request.Callback(obj);
            }
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static T CallMethod<T>(IpcChannel channel, int id, params object[] arg)
        {
            mre.Reset();
            T ret = default(T);
            Optional<string> errorMessage = false;

            channel.communicator.DataReceived += (s, e) =>
              {
                  var resp = JsonConvert.DeserializeObject<FunctionCallResponse>(Encoding.ASCII.GetString(e.Data));

                  if (true)//resp.ID == id)
                  {
                      if (string.IsNullOrEmpty(resp.ErrorMessage))
                      {
                          if (resp.ReturnValue != null)
                          {
                              ret = JsonConvert.DeserializeObject<T>(resp.ReturnValue);
                          }
                      }
                      else
                      {
                          errorMessage = resp.ErrorMessage.ToOptional();
                      }

                      mre.Set();
                  }
              };

            var m = new FunctionCallRequest
            {
                ID = id,
                ParameterJson = arg.Select(_ => JsonConvert.SerializeObject(_)).ToArray()
            };

            var raw = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(m));

            channel.communicator.Write(raw);

            mre.WaitOne();

            if (errorMessage)
            {
                throw new Exception(errorMessage);
            }

            return ret;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static Signature GetSignatureOf(IpcChannel channel, int id)
        {
            return CallMethod<Signature>(channel, (int)MethodConstants.GetSignature, id);
        }

        internal enum MethodConstants : int
        {
            GetSignature = 316497852
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
            var json = JsonConvert.SerializeObject(msg);
            var raw = Encoding.ASCII.GetBytes(json);

            channel.communicator.Write(raw);
        }

        public static void CallEvent<EventType>(IpcChannel channel, EventType et)
        {
            var objid = typeof(EventType).GUID;
            var serialized = JsonConvert.SerializeObject(et);

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write(objid.ToByteArray());
            bw.Write(serialized);

            var raw = ms.ToArray();

            channel.event_communicator.Write(raw);
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
                    {
                        foreach (var m in t.GetMethods())
                        {
                            var mattr = m.GetCustomAttribute<SharedFunctionAttribute>();

                            if (mattr != null)
                            {
                                channel.shared_functions.Add(mattr.ID, m);
                                channel.communicator.DataReceived += (s, e) =>
                                 {
                                     var obj = JsonConvert.DeserializeObject<FunctionCallRequest>(Encoding.ASCII.GetString(e.Data));

                                     Optional<string> error = false;
                                     object res = null;
                                     object[] args = null;

                                     var methodInfo = channel.shared_functions[obj.ID];
                                     var parameterInfo = methodInfo.GetParameters();

                                     if (!IsArgumentMismatch(parameterInfo, obj.ParameterJson))
                                     {
                                         args = GetDeserializedParameters(parameterInfo, obj.ParameterJson);
                                     }

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

                                     var resp = new FunctionCallResponse
                                     {
                                         ID = obj.ID
                                     };

                                     if (!error)
                                     {
                                         resp.ReturnValue = JsonConvert.SerializeObject(res);
                                     }
                                     else
                                     {
                                         resp.ErrorMessage = error;
                                     }

                                     var raw = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(resp));
                                     channel.communicator.Write(raw);
                                 };
                            }
                        }
                    }
                }
            }
        }

        private static bool IsArgumentMismatch(ParameterInfo[] parameterInfo, string[] parameterJson)
        {
            return parameterInfo.Length != parameterJson.Length;
        }

        private static object[] GetDeserializedParameters(ParameterInfo[] parameterInfo, string[] parameterJson)
        {
            var res = new List<object>();
            for (int i = 0; i < parameterJson.Length; i++)
            {
                res.Add(JsonConvert.DeserializeObject(parameterJson[i], parameterInfo[i].ParameterType));
            }
            return res.ToArray();
        }

        public static SharedObject<T> CreateSharedObject<T>(int id, bool sender = false)
        {
            if (sender)
            {
                return SharedObject<T>.CreateSender(id);
            }

            return SharedObject<T>.CreateReciever(id);
        }
    }
}