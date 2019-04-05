using System;
using System.Collections.Generic;
using System.Linq;

namespace Furesoft.Signals
{
    public class Pipeline<TArg>
    {
        private List<Func<TArg, TArg>> _items = new List<Func<TArg, TArg>>();

        public void AddToEnd(Func<TArg, TArg> callback)
        {
            _items.Add(callback);
        }

        public void AddToStart(Func<TArg, TArg> callback)
        {
            _items.Insert(0, callback);
        }

        public void AddBefore(int index, Func<TArg, TArg> callback)
        {
            _items.Insert(index, callback);
        }

        internal TArg Invoke(TArg arg)
        {
            var res = default(TArg);

            if (!_items.Any())
            {
                return arg;
            }

            foreach (var cb in _items)
            {
                res = cb(arg);
            }

            return res;
        }

        public static TArg operator +(Pipeline<TArg> target, TArg value)
        {
            return target.Invoke(value);
        }
    }
}