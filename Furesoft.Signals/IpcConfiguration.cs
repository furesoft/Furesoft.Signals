using System;
using System.Collections.Generic;
using System.Reflection;

namespace Furesoft.Signals
{
    public class IpcConfiguration
    {
        public void AddSharedFunction(int id, Delegate mi)
        {
            shared_functions.Add(id, mi.GetMethodInfo());
        }

        internal Dictionary<int, MethodInfo> shared_functions = new Dictionary<int, MethodInfo>();
    }
}