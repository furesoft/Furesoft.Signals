using Furesoft.Signals.Core;
using Furesoft.Signals.Messages;
using System;
using System.Collections.Generic;

namespace Furesoft.Signals
{
    public class Queue
    {
        MemoryMappedFileCommunicator _com;
        private List<QueueMessage> _messages = new List<QueueMessage>();

        public static Queue CreateProducer(string name)
        {
            var q = new Queue();
            q._com = new MemoryMappedFileCommunicator(name, 4096);
            q._com.ReadPosition = 2000;
            q._com.WritePosition = 0;
            q._com.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<QueueMessage>(e.Data);
                q._messages.Add(o);
            };

            q._com.StartReader();

            return q;
        }

        public static Queue CreateConsumer(string name)
        {
            var q = new Queue();
            q._com = new MemoryMappedFileCommunicator(name, 4096);
            q._com.ReadPosition = 0;
            q._com.WritePosition = 2000;
            q._com.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<QueueMessage>(e.Data);
                q._messages.Add(o);
            };

            q._com.StartReader();

            return q;
        }

        public void Subscribe<T>(Action<T> callback)
        {

        }

        public void Publish<T>(T obj)
        {
            var msg = new QueueMessage();
            msg.Typename = typeof(T).Name;
            msg.Raw = Signal.Serializer.Serialize(obj);

            _com.Write(Signal.Serializer.Serialize(msg));
        }
    }
}