using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Furesoft.Signals.Core;
using Newtonsoft.Json;

namespace Furesoft.Signals
{
    public static class Signal
    {
        private static Queue<RecieveRequest> recieveQueue = new Queue<RecieveRequest>();

        public static Pipeline<IpcMessage> BeforeSignal = new Pipeline<IpcMessage>();
        public static Pipeline<object> AfterSignal = new Pipeline<object>();

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

            channel.communicator.DataReceived += new EventHandler<DataReceivedEventArgs>(Pipeline_DataReceived);
            channel.communicator.StartReader();

            channel.event_communicator = new MemoryMappedFileCommunicator(name + ".events", 4096);
            channel.event_communicator.ReadPosition = 2000;
            channel.event_communicator.WritePosition = 0;

            channel.event_communicator.StartReader();

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
            channel.communicator.DataReceived += new EventHandler<DataReceivedEventArgs>(Pipeline_DataReceived);
            channel.communicator.StartReader();

            //initialize event communicator
            channel.event_communicator = new MemoryMappedFileCommunicator(name + ".events", 4096);
            channel.event_communicator.WritePosition = 2000;
            channel.event_communicator.ReadPosition = 0;
            channel.event_communicator.StartReader();

            return channel;
        }

        public static IpcChannel CreateRecieverChannel(int name)
        {
            return CreateRecieverChannel(name.ToString());
        }

        private static void Pipeline_DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (recieveQueue.Count > 0)
            {
                var request = recieveQueue.Dequeue();
                var obj = JsonConvert.DeserializeObject(Encoding.ASCII.GetString(e.Data), request.Type);

                obj = AfterSignal.Invoke(obj);

                request.Callback(obj);
            }
        }

        public static void Recieve<T>(Action<T> callback)
        {
            recieveQueue.Enqueue(
                new RecieveRequest {
                    Type = typeof(T),
                    Callback = new Action<object>(
                        o => callback((T)o)),
                    Name = typeof(T).Name
                });
        }

        public static void Send(IpcChannel channel, IpcMessage msg)
        {
            msg = BeforeSignal.Invoke(msg);

            var json = JsonConvert.SerializeObject(msg);

            channel.communicator.Write(json);
        }

        public static void CallEvent<EventType>(IpcChannel channel, EventType et)
        {
            var objid = typeof(EventType).GUID;
            var serialized = JsonConvert.SerializeObject(et);

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write(objid.ToByteArray());
            bw.Write(serialized);

            channel.event_communicator.Write(ms.ToArray());
        }

        public static SharedObject<T> CreateSharedObject<T>(int id, bool sender = false)
        {
            if(sender)
            {
                return SharedObject<T>.CreateSender(id);
            }

            return SharedObject<T>.CreateReciever(id);
        }
    }
}