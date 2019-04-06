using System;

namespace Furesoft.Signals
{
    public interface ISerializer
    {
        byte[] Serialize(object obj);

        object Deserialize(byte[] raw, Type type);
    }

    public static class ISerializerExtensions
    {
        public static T Deserialize<T>(this ISerializer s, byte[] raw)
        {
            return (T)s.Deserialize(raw, typeof(T));
        }
    }
}