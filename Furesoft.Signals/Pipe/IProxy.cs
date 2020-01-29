using System;

namespace Furesoft.Signals.Pipe
{
    internal interface IProxy
    {
        FastChannel Channel { get; }

        byte DomainAddress { get; }

        bool IsDisconnected { get; }

        /// <summary>Nongeneric version of the LocalInstance </summary>
        object LocalInstanceUntyped { get; }

        int? ObjectID { get; }
        Type ObjectType { get; }

        void Disconnect();

        /// <summary>Connects the proxy for channel implementors. Used by FastChannel.</summary>
        void RegisterLocal(FastChannel fastChannel, int? objectID, Action onDisconnect);
    }
}