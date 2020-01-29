using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    internal class FastChannel : AsyncDisposable
    {
        public readonly byte DomainAddress;
        public int MessagesReceived { get { return _messagesReceived; } }

        public FastChannel(string name, bool isOwner, Module module)
        {
            _module = module;  // Types belonging to this module will serialize faster

            DomainAddress = (byte)(isOwner ? 1 : 2);

            _outPipe = new OutPipe(name + (isOwner ? ".A" : ".B"), isOwner);
            _inPipe = new InPipe(name + (isOwner ? ".B" : ".A"), isOwner, OnMessageReceived);
        }

        /// <summary>Evalulates an expression tree quickly on the local side without the cost of calling Compile().
        /// This works only with simple method calls and property reads. In other cases, it returns null.</summary>
        public static Func<T, U> FastEvalExpr<T, U>(Expression body)
        {
            // Optimize common cases:
            MethodInfo method;
            IEnumerable<Expression> args = new Expression[0];
            var mc = body as MethodCallExpression;
            if (mc != null)
            {
                method = mc.Method;
                args = mc.Arguments;
            }
            else
            {
                var me = body as MemberExpression;
                if (me != null && me.Member is PropertyInfo)
                    method = ((PropertyInfo)me.Member).GetGetMethod();
                else
                    return null;
            }
            return x =>
            {
                try
                {
                    return (U)method.Invoke(x, args.Select(a => GetExprValue(a, false)).ToArray());
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            };
        }

        /// <summary>Instantiates an object remotely. To release it, you can either call Disconnect on the proxy returned
        /// or wait for its finalizer to do the same.s</summary>
        public Task<Proxy<TRemote>> Activate<TRemote>() where TRemote : class
        {
            using (DeferDisposal())
            {
                int messageNumber = Interlocked.Increment(ref _lastMessageID);

                var ms = new MemoryStream();
                var writer = new BinaryWriter(ms);
                writer.Write((byte)MessageType.Activation);
                writer.Write(messageNumber);
                SerializeType(writer, typeof(TRemote));
                writer.Flush();

                var task = GetResponseFuture<Proxy<TRemote>>(messageNumber);
                _outPipe.Write(ms.ToArray());

                return task;
            }
        }

        public async override Task DisposeAsync()
        {
            lock (_proxiesLock)
            {
                _proxiesByID.Clear();
                _proxiesByObject.Clear();
            }
            await _inPipe.DisposeAsync().ConfigureAwait(false);
            await _outPipe.DisposeAsync().ConfigureAwait(false);
        }

        internal void InternalDeactivate(int objectID)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write((byte)MessageType.Deactivation);
            writer.Write(objectID);
            writer.Flush();

            using (var token = TryDeferDisposal())
            {
                if (token == null) return;
                _outPipe.Write(ms.ToArray());
            }
        }

        internal Task<TResult> SendMethodCall<TResult>(Expression expressionBody, int objectID, bool awaitRemoteTask)
        {
            using (DeferDisposal())
            {
                int messageNumber = Interlocked.Increment(ref _lastMessageID);
                byte[] payload = SerializeMethodCall(expressionBody, messageNumber, objectID, awaitRemoteTask);
                var task = GetResponseFuture<TResult>(messageNumber);
                _outPipe.Write(payload);
                return task;
            }
        }

        private static int _lastMessageID, _lastObjectID;
        private readonly InPipe _inPipe;
        private readonly Module _module;
        private readonly OutPipe _outPipe;
        private readonly Dictionary<int, Action<MessageType, object>> _pendingReplies = new Dictionary<int, Action<MessageType, object>>();

        private readonly Dictionary<int, IProxy> _proxiesByID = new Dictionary<int, IProxy>();

        private readonly Dictionary<object, IProxy> _proxiesByObject = new Dictionary<object, IProxy>();

        // The module for which serialization is optimized.
        private readonly object _proxiesLock = new object();

        private int _messagesReceived;

        private enum FastTypeCode : byte
        { Null, False, True, Byte, Char, String, Int32, Proxy, Other }

        private enum MessageType : byte
        { Activation, Deactivation, MethodCall, ReturnValue, ReturnException }

        private static object GetExprValue(Expression expr, bool deferLocalInstanceProperty)
        {
            // Optimize the common simple cases, the first being a simple constant:
            var constant = expr as ConstantExpression;
            if (constant != null) return constant.Value;

            // The second common simple case is accessing a field in a closure:
            var me = expr as MemberExpression;

            if (me != null && me.Member is FieldInfo && me.Expression is ConstantExpression)
                return ((FieldInfo)me.Member).GetValue(((ConstantExpression)me.Expression).Value);

            // If we're referring to the LocalInstance property of the proxy, we need to defer its evaluation
            // until it's deserialized at the other end, as it will likely be null:
            if (deferLocalInstanceProperty && me != null && me.Member is PropertyInfo)
            {
                if (me.Member.Name == "LocalInstance" &&
                    me.Member.ReflectedType.IsGenericType &&
                    me.Member.ReflectedType.GetGenericTypeDefinition() == typeof(Proxy<>))
                {
                    return GetExprValue(me.Expression, true);
                }
            }

            // This will take longer:
            var objectMember = Expression.Convert(expr, typeof(object));
            var getterLambda = Expression.Lambda<Func<object>>(objectMember);
            var getter = getterLambda.Compile();
            return getter();
        }

        private IEnumerable<object> DeserializeArguments(BinaryReader reader, ParameterInfo[] args)
        {
            byte objectCount = reader.ReadByte();
            for (int i = 0; i < objectCount; i++) yield return DeserializeValue(reader, args[i].ParameterType);
        }

        private MethodBase DeserializeMethod(BinaryReader reader)
        {
            int b = reader.ReadByte();
            if (b == 1)
                return _module.ResolveMethod(reader.ReadInt32());
            else
                return Type.GetType(reader.ReadString(), true).Module.ResolveMethod(reader.ReadInt32());
        }

        private Type DeserializeType(BinaryReader reader)
        {
            int b = reader.ReadByte();
            if (b == 1)
                return _module.ResolveType(reader.ReadInt32());
            else
                return Type.GetType(reader.ReadString());
        }

        private object DeserializeValue(BinaryReader reader, Type expectedType = null)
        {
            var typeCode = (FastTypeCode)reader.ReadByte();
            if (typeCode == FastTypeCode.Null) return null;
            if (typeCode == FastTypeCode.False) return false;
            if (typeCode == FastTypeCode.True) return true;
            if (typeCode == FastTypeCode.Byte) return reader.ReadByte();
            if (typeCode == FastTypeCode.Char) return reader.ReadChar();
            if (typeCode == FastTypeCode.String) return reader.ReadString();
            if (typeCode == FastTypeCode.Int32) return reader.ReadInt32();
            if (typeCode == FastTypeCode.Proxy)
            {
                Type genericType = DeserializeType(reader);
                Type actualType = DeserializeType(reader);
                int objectID = reader.ReadInt32();
                byte domainAddress = reader.ReadByte();
                if (domainAddress == DomainAddress)   // We own the real object
                {
                    var proxy = FindProxy(objectID);
                    if (proxy == null)
                        throw new ObjectDisposedException("Cannot deserialize type '" + genericType.Name + "' - object has been disposed");

                    // Automatically unmarshal if necessary:
                    if (expectedType != null && expectedType.IsInstanceOfType(proxy.LocalInstanceUntyped)) return proxy.LocalInstanceUntyped;

                    return proxy;
                }
                // The other domain owns the object.
                var proxyType = typeof(Proxy<>).MakeGenericType(genericType);
                return Activator.CreateInstance(proxyType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[]
                {
                    this,
                    objectID,
                    domainAddress,
                     actualType
                }, null);
            }
            return new BinaryFormatter().Deserialize(reader.BaseStream);
        }

        private IProxy FindProxy(int objectID)
        {
            IProxy proxy;
            lock (_proxiesLock) _proxiesByID.TryGetValue(objectID, out proxy);
            return proxy;
        }

        private Task<T> GetResponseFuture<T>(int messageNumber)
        {
            var tcs = new TaskCompletionSource<T>();

            lock (_pendingReplies)
                _pendingReplies.Add(messageNumber, (msgType, value) =>
                {
                    if (msgType == MessageType.ReturnValue)
                        tcs.SetResult((T)value);
                    else
                    {
                        var ex = (Exception)value;
                        MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (preserveStackTrace != null) preserveStackTrace.Invoke(ex, null);
                        tcs.SetException(ex);
                    }
                });

            return tcs.Task;
        }

        private void OnMessageReceived(byte[] data)
        {
            Interlocked.Increment(ref _messagesReceived);
            using (var token = TryDeferDisposal())
            {
                if (token == null) return;  // We've been disposed

                var ms = new MemoryStream(data);
                var reader = new BinaryReader(ms);

                var messageType = (MessageType)reader.ReadByte();

                if (messageType == MessageType.ReturnValue || messageType == MessageType.ReturnException)
                    ReceiveReply(messageType, reader);
                else if (messageType == MessageType.MethodCall)
                    ReceiveMethodCall(reader);
                else if (messageType == MessageType.Activation)
                    ReceiveActivation(reader);
                else if (messageType == MessageType.Deactivation)
                    ReceiveDeactivation(reader);
            }
        }

        private void ReceiveActivation(BinaryReader reader)
        {
            int messageNumber = reader.ReadInt32();
            object instance = null;
            Exception error = null;
            try
            {
                var type = DeserializeType(reader);
                instance = Activator.CreateInstance(type, true);
                var proxy = (IProxy)Activator.CreateInstance(typeof(Proxy<>).MakeGenericType(type), BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { instance, DomainAddress }, null);
                instance = RegisterLocalProxy(proxy);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            SendReply(messageNumber, instance, error, false);
        }

        private void ReceiveDeactivation(BinaryReader reader)
        {
            int objectID = reader.ReadInt32();
            lock (_proxiesLock)
            {
                var proxy = FindProxy(objectID);
                if (proxy == null) return;
                proxy.Disconnect();
            }
        }

        private void ReceiveMethodCall(BinaryReader reader)
        {
            int messageNumber = reader.ReadInt32();
            int objectID = reader.ReadInt32();
            bool awaitRemoteTask = reader.ReadBoolean();
            var method = DeserializeMethod(reader);

            Exception error = null;
            object returnValue = null;
            object[] args = null;
            IProxy proxy = null;

            try
            {
                args = DeserializeArguments(reader, method.GetParameters()).ToArray();
                proxy = FindProxy(objectID);
                if (proxy == null) throw new ObjectDisposedException("Proxy " + objectID + " has been disposed.");
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Task.Factory.StartNew(() =>
            {
                if (error == null)
                    try
                    {
                        var instance = proxy.LocalInstanceUntyped;
                        if (instance == null)
                        {
                            string typeInfo = proxy.ObjectType == null ? "?" : proxy.ObjectType.FullName;
                            error = new ObjectDisposedException("Proxy " + objectID + " is disposed. Type: " + typeInfo);
                        }
                        else returnValue = method.Invoke(instance, args);
                    }
                    catch (Exception ex)
                    {
                        if (ex is TargetInvocationException) error = ex.InnerException;
                        else error = ex;
                    }

                SendReply(messageNumber, returnValue, error, awaitRemoteTask);
            }, TaskCreationOptions.PreferFairness);
        }

        private void ReceiveReply(MessageType messageType, BinaryReader reader)
        {
            int msgNumber = reader.ReadInt32();
            object value = DeserializeValue(reader);
            Action<MessageType, object> reply;
            lock (_pendingReplies)
            {
                if (!_pendingReplies.TryGetValue(msgNumber, out reply)) return;   // Orphan reply
                _pendingReplies.Remove(msgNumber);
            }
            reply(messageType, value);
        }

        private IProxy RegisterLocalProxy(IProxy proxy)
        {
            lock (_proxiesLock)
            {
                // Avoid multiple identities for the same object:
                IProxy existingProxy;
                bool alreadyThere = _proxiesByObject.TryGetValue(proxy.LocalInstanceUntyped, out existingProxy);
                if (alreadyThere) return existingProxy;

                int objectID = Interlocked.Increment(ref _lastObjectID);
                proxy.RegisterLocal(this, objectID, () => UnregisterLocalProxy(proxy, objectID));

                _proxiesByID[objectID] = proxy;
                _proxiesByObject[proxy] = proxy;
                return proxy;
            }
        }

        private void SendReply(int messageNumber, object returnValue, Exception error, bool awaitRemoteTask)
        {
            using (var token = TryDeferDisposal())
            {
                if (token == null) return;
                if (awaitRemoteTask)
                {
                    var returnTask = (Task)returnValue;
                    // The method we're calling is itself asynchronous. Delay sending a reply until the method itself completes.
                    returnTask.ContinueWith(ant => SendReply(messageNumber, ant.IsFaulted ? null : ant.GetUntypedResult(), ant.Exception, false));
                    return;
                }
                var ms = new MemoryStream();
                var writer = new BinaryWriter(ms);
                writer.Write((byte)(error == null ? MessageType.ReturnValue : MessageType.ReturnException));
                writer.Write(messageNumber);
                SerializeValue(writer, error ?? returnValue);
                writer.Flush();
                _outPipe.Write(ms.ToArray());
            }
        }

        private void SerializeArguments(BinaryWriter writer, object[] args)
        {
            writer.Write((byte)args.Length);
            foreach (var o in args) SerializeValue(writer, o);
        }

        private void SerializeMethod(BinaryWriter writer, MethodInfo mi)
        {
            if (mi.Module == _module)
            {
                writer.Write((byte)1);
                writer.Write(mi.MetadataToken);
            }
            else
            {
                writer.Write((byte)2);
                writer.Write(mi.DeclaringType.AssemblyQualifiedName);
                writer.Write(mi.MetadataToken);
            }
        }

        private byte[] SerializeMethodCall(Expression expr, int messageNumber, int objectID, bool awaitRemoteTask)
        {
            if (expr == null) throw new ArgumentNullException("expr");

            MethodInfo method;
            IEnumerable<Expression> args = new Expression[0];
            var mc = expr as MethodCallExpression;
            if (mc != null)
            {
                method = mc.Method;
                args = mc.Arguments;
            }
            else
            {
                var me = expr as MemberExpression;
                if (me != null && me.Member is PropertyInfo)
                    method = ((PropertyInfo)me.Member).GetGetMethod();
                else
                    throw new InvalidOperationException("Only method calls and property reads can be serialized");
            }

            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write((byte)MessageType.MethodCall);
            writer.Write(messageNumber);
            writer.Write(objectID);
            writer.Write(awaitRemoteTask);
            SerializeMethod(writer, method);
            SerializeArguments(writer, args.Select(a => GetExprValue(a, true)).ToArray());
            writer.Flush();
            return ms.ToArray();
        }

        private void SerializeType(BinaryWriter writer, Type t)
        {
            if (t.Module == _module)
            {
                writer.Write((byte)1);
                writer.Write(t.MetadataToken);
            }
            else
            {
                writer.Write((byte)2);
                writer.Write(t.AssemblyQualifiedName);
            }
        }

        private void SerializeValue(BinaryWriter writer, object o)
        {
            if (o == null)
            {
                writer.Write((byte)FastTypeCode.Null);
            }
            else if (o is bool)
            {
                writer.Write((byte)((bool)o ? FastTypeCode.True : FastTypeCode.False));
            }
            else if (o is byte)
            {
                writer.Write((byte)FastTypeCode.Byte);
                writer.Write((byte)o);
            }
            else if (o is char)
            {
                writer.Write((byte)FastTypeCode.Char);
                writer.Write((char)o);
            }
            else if (o is string)
            {
                writer.Write((byte)FastTypeCode.String);
                writer.Write((string)o);
            }
            else if (o is int)
            {
                writer.Write((byte)FastTypeCode.Int32);
                writer.Write((int)o);
            }
            else if (o is IProxy)
            {
                writer.Write((byte)FastTypeCode.Proxy);
                var proxy = (IProxy)o;
                if (proxy.LocalInstanceUntyped != null) proxy = RegisterLocalProxy(proxy);
                var typeArgType = o.GetType().GetGenericArguments()[0];
                SerializeType(writer, typeArgType);
                SerializeType(writer, proxy.LocalInstanceUntyped == null ? typeArgType : proxy.LocalInstanceUntyped.GetType());
                writer.Write(proxy.ObjectID.Value);
                // The domain address will be zero if created via implicit conversion.
                writer.Write(proxy.DomainAddress == 0 ? DomainAddress : proxy.DomainAddress);
            }
            else
            {
                writer.Write((byte)FastTypeCode.Other);
                writer.Flush();
                new BinaryFormatter().Serialize(writer.BaseStream, o);
            }
        }

        private void UnregisterLocalProxy(IProxy proxy, int objectID)
        {
            lock (_proxiesLock)
            {
                _proxiesByID.Remove(objectID);
                _proxiesByObject.Remove(proxy.LocalInstanceUntyped);
            }
        }
    }
}