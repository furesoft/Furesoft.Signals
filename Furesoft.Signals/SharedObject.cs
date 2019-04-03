using System;
using System.Collections.Generic;

namespace Furesoft.Signals
{
    public class SharedObject<T>
    {
        private List<Action<T>> _callbacks = new List<Action<T>>();

        public void OnChanged(Action<T> callback)
        {
            _callbacks.Add(callback);
        }

        public void SetValue(T value)
        {

        }

        public static SharedObject<T> operator +(SharedObject<T> obj, Action<T> callback)
        {
            obj.OnChanged(callback);

            return obj;
        }
    }
}