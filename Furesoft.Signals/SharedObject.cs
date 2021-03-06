﻿using Furesoft.Signals.Core;
using System;
using System.Collections.Generic;

namespace Furesoft.Signals
{
    public sealed class SharedObject<T> : IDisposable
    {
        public int ID { get; private set; }

        public static SharedObject<T> operator +(SharedObject<T> obj, Action<T> callback)
        {
            obj.OnChanged(callback);

            return obj;
        }

        public static SharedObject<T> operator +(SharedObject<T> obj, T value)
        {
            obj.SetValue(value);

            return obj;
        }

        public void Dispose()
        {
            communicator.Dispose();
            communicator = null;
        }

        public void OnChanged(Action<T> callback)
        {
            _callbacks.Add(callback);
        }

        public void SetValue(T value)
        {
            var raw = Signal.Serializer.Serialize(value);
            communicator.Write(raw);
        }

        internal static SharedObject<T> CreateReciever(int id)
        {
            var obj = new SharedObject<T>();
            obj.ID = id;

            obj.communicator = new MemoryMappedFileCommunicator($"{id.ToString()}.shared", 4096);
            obj.communicator.ReadPosition = 0;
            obj.communicator.WritePosition = 2000;
            obj.communicator.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<T>(e.Data);

                obj._callbacks.ForEach(_ =>
                {
                    _(o);
                });
            };

            obj.communicator.StartReader();

            return obj;
        }

        internal static SharedObject<T> CreateSender(int id)
        {
            var obj = new SharedObject<T>();
            obj.ID = id;

            obj.communicator = new MemoryMappedFileCommunicator($"{id.ToString()}.shared", 4096);
            obj.communicator.ReadPosition = 2000;
            obj.communicator.WritePosition = 0;
            obj.communicator.DataReceived += (s, e) =>
            {
                var o = Signal.Serializer.Deserialize<T>(e.Data);

                obj._callbacks.ForEach(_ =>
                {
                    _(o);
                });
            };

            obj.communicator.StartReader();

            return obj;
        }

        private List<Action<T>> _callbacks = new List<Action<T>>();
        private MemoryMappedFileCommunicator communicator;
    }
}