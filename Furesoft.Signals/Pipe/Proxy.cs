using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    /// <summary>
    /// Wraps a reference to an object that potentially lives in another domain. This ensures that cross-domain calls are explicit in the
    /// source code, and allows for a transport other than .NET Remoting (e.g., FastChannel).
    /// </summary>
    internal class Proxy<TRemote> : IProxy where TRemote : class
    {
        /// <summary>This is populated if the proxy was obtained via FastChannel instead of Remoting.</summary>
        public FastChannel Channel { get; private set; }

        /// <summary>Identifies the domain that owns the local instance, when FastChannel is in use.</summary>
        public byte DomainAddress { get; private set; }

        public bool IsDisconnected { get { return LocalInstance == null && Channel == null; } }

        /// <summary>The object being Remoted. This is populated only on the local side.</summary>
        public TRemote LocalInstance { get; private set; }

        object IProxy.LocalInstanceUntyped { get { return LocalInstance; } }

        /// <summary>This is populated if the proxy was obtained via FastChannel instead of Remoting. It uniquely identifies the
        /// object within the channel if the object is connected (has a presence in the other domain).</summary>
        public int? ObjectID { get; private set; }

        /// <summary>The real type of the object being proxied. This may be a subclass or derived implementation of TRemote.</summary>
        public Type ObjectType { get { return _actualObjectType ?? (LocalInstance != null ? LocalInstance.GetType() : typeof(TRemote)); } }

        public Proxy(TRemote instance)
        {
            LocalInstance = instance;
        }

        /// <summary>Any reference-type object can be implicitly converted to a proxy. The proxy will become connected
        /// automatically when it's serialized during a remote method call.</summary>
        public static implicit operator Proxy<TRemote>(TRemote instance)
        {
            if (instance == null) return null;
            return new Proxy<TRemote>(instance);
        }

        public void AssertRemote()
        {
            if (LocalInstance != null)
                throw new InvalidOperationException("Object " + LocalInstance.GetType().Name + " is not remote");
        }

        /// <summary>This is useful when this.ObjectType is a subclass or derivation of TRemote.</summary>
        public Proxy<TNew> CastTo<TNew>() where TNew : class, TRemote
        {
            if (!typeof(TNew).IsAssignableFrom(ObjectType))
                throw new InvalidCastException("Type '" + ObjectType.FullName + "' cannot be cast to '" + typeof(TNew).FullName + "'.");

            return new Proxy<TNew>(this, _onDisconnect, _actualObjectType);
        }

        public void Disconnect()
        {
            Action onDisconnect;
            lock (_locker)
            {
                onDisconnect = _onDisconnect;
                _onDisconnect = null;
            }

            if (onDisconnect != null) onDisconnect();

            // If the remote reference drops away, we should ensure that it gets release on the other domain as well:

            lock (_locker)
            {
                if (Channel == null || LocalInstance != null || ObjectID == null)
                    LocalInstance = null;
                else
                    Channel.InternalDeactivate(ObjectID.Value);

                Channel = null;
                ObjectID = null;
            }
        }

        /// <summary>Runs a non-void method on the object being proxied. This works on both the local and remote side.</summary>
        public Task<TResult> Eval<TResult>(Expression<Func<TRemote, TResult>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, TResult>(remoteMethod.Body);
                    if (fastEval != null) return Task.FromResult(fastEval(li));
                    return Task.FromResult(remoteMethod.Compile()(li));
                }
                catch (Exception ex)
                {
                    return Task.FromException<TResult>(ex);
                }
            return SendMethodCall<TResult>(remoteMethod.Body, false);
        }

        /// <summary>Runs a non-void method on the object being proxied. This works on both the local and remote side.
        /// Use this overload for methods on the other domain that are themselves asynchronous.</summary>
        public Task<TResult> Eval<TResult>(Expression<Func<TRemote, Task<TResult>>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, Task<TResult>>(remoteMethod.Body);
                    if (fastEval != null) return fastEval(li);
                    return remoteMethod.Compile()(li);
                }
                catch (Exception ex)
                {
                    return Task.FromException<TResult>(ex);
                }
            return SendMethodCall<TResult>(remoteMethod.Body, true);
        }

        void IProxy.RegisterLocal(FastChannel fastChannel, int? objectID, Action onDisconnect)
        {
            // This is called by FastChannel to connect/register the proxy.
            lock (_locker)
            {
                Channel = fastChannel;
                ObjectID = objectID;
                DomainAddress = fastChannel.DomainAddress;
                _onDisconnect = onDisconnect;
            }
        }

        /// <summary>Runs a (void) method on the object being proxied. This works on both the local and remote side.</summary>
        public Task Run(Expression<Action<TRemote>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, object>(remoteMethod.Body);
                    if (fastEval != null) fastEval(li);
                    else remoteMethod.Compile()(li);
                    return Task.FromResult(false);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            return SendMethodCall<object>(remoteMethod.Body, false);
        }

        /// <summary>Runs a (void) method on the object being proxied. This works on both the local and remote side.
        /// Use this overload for methods on the other domain that are themselves asynchronous.</summary>
        public Task Run(Expression<Func<TRemote, Task>> remoteMethod)
        {
            var li = LocalInstance;
            if (li != null)
                try
                {
                    var fastEval = FastChannel.FastEvalExpr<TRemote, Task>(remoteMethod.Body);
                    if (fastEval != null) return fastEval(li);
                    return remoteMethod.Compile()(li);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            return SendMethodCall<object>(remoteMethod.Body, true);
        }

        private readonly Type _actualObjectType;
        private readonly object _locker = new object();
        private Action _onDisconnect;

        private Proxy(IProxy conversionSource, Action onDisconnect, Type actualObjectType)
        {
            LocalInstance = (TRemote)conversionSource.LocalInstanceUntyped;
            Channel = conversionSource.Channel;
            ObjectID = conversionSource.ObjectID;
            DomainAddress = conversionSource.DomainAddress;
            _onDisconnect = onDisconnect;
            _actualObjectType = actualObjectType;
        }

        // Called via reflection:
        private Proxy(TRemote instance, byte domainAddress)
        { LocalInstance = instance; DomainAddress = domainAddress; }

        // Called via reflection:
        private Proxy(FastChannel channel, int objectID, byte domainAddress, Type actualInstanceType)
        {
            Channel = channel;
            ObjectID = objectID;
            DomainAddress = domainAddress;
            _actualObjectType = actualInstanceType;
        }

        ~Proxy()
        {
            if (_locker != null)
                try
                {
                    Disconnect();
                }
                catch (ObjectDisposedException) { }
        }

        private Task<TResult> SendMethodCall<TResult>(Expression expressionBody, bool awaitRemoteTask)
        {
            FastChannel channel;
            int? objectID;
            lock (_locker)
            {
                if (Channel == null)
                    return Task.FromException<TResult>(new InvalidOperationException("Channel has been disposed on Proxy<" + typeof(TRemote).Name + "> " + expressionBody));

                channel = Channel;
                objectID = ObjectID;
            }
            return channel.SendMethodCall<TResult>(expressionBody, objectID.Value, awaitRemoteTask);
        }
    }
}