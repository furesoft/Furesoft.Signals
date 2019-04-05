using System;

namespace Furesoft.Signals.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SharedFunctionAttribute : Attribute
    {
        public int ID { get; }

        public SharedFunctionAttribute(int id)
        {
            ID = id;
        }
    }
}