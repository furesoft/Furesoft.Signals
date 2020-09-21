using Furesoft.Signals.Core;
using Furesoft.Signals.Messages;
using System;
using System.Collections.Generic;

namespace Furesoft.Signals
{
    public class MessageQueue
    {
        private MemoryMappedFileCommunicator _com;
        private List<MessageQueueHandler> _handlers = new List<MessageQueueHandler>();

        public void Subscribe<T>(Action<T> callback)
        {
            var handler = new MessageQueueHandler { Typename = typeof(T).Name, Action = callback, Type = typeof(T) };
            _handlers.Add(handler);
        }

        public void Echo<T>()
        {
            Subscribe<T>(_ =>
            {
                Publish<T>(_);
            });
        }

        public static MessageQueue CreateProducer(string name)
        {
            var q = new MessageQueue();
            q._com = new MemoryMappedFileCommunicator(name, 4096);
            q._com.ReadPosition = 2000;
            q._com.WritePosition = 0;
            q._com.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<QueueMessage>(e.Data);
                foreach (var h in q._handlers)
                {
                    if (h.Typename == o.Typename)
                    {
                        h.Action.DynamicInvoke(Signal.Serializer.Deserialize(o.Argument, h.Type));
                    }
                }
            };

            q._com.StartReader();

            return q;
        }

        public static MessageQueue CreateConsumer(string name)
        {
            var q = new MessageQueue();
            q._com = new MemoryMappedFileCommunicator(name, 4096);
            q._com.ReadPosition = 0;
            q._com.WritePosition = 2000;
            q._com.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<QueueMessage>(e.Data);
                foreach (var h in q._handlers)
                {
                    if (h.Typename == o.Typename)
                    {
                        h.Action.DynamicInvoke(Signal.Serializer.Deserialize(o.Argument, h.Type));
                    }
                }
            };

            q._com.StartReader();

            return q;
        }

        public void Publish<T>(T obj)
        {
            var msg = new QueueMessage();
            msg.Typename = typeof(T).Name;
            msg.Argument = Signal.Serializer.Serialize(obj);

            _com.Write(Signal.Serializer.Serialize(msg));
        }
    }
}