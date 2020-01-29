using System;
using System.Threading;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    public class AsyncDisposable : IDisposable
    {
        public bool IsDisposed => _deferralEvent.IsSet;
        public bool IsDisposeRequested { get; private set; }

        public void AssertSafe()
        {
            lock (_locker)
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().Name + " has been disposed");

                if (_deferralEvent.CurrentCount < 2 && !IsDisposeRequested)
                    throw new InvalidOperationException(GetType().Name + " has not had disposal deferred.");
            }
        }

        /// <summary>Suspends disposal until the token that it returns is disposed.
        /// If already disposed, throws an ObjectDisposedException.</summary>
        public IDisposable DeferDisposal()
        {
            if (!_deferralEvent.TryAddCount())
                throw new ObjectDisposedException(GetType().Name);

            return Disposable.Create(() => _deferralEvent.Signal());
        }

        public async void Dispose() => await DisposeAsync().ConfigureAwait(false);

        /// <summary>Starts disposal. If disposal has been deferred, disposal will not start
        /// until the deferral tokens have been released.</summary>
        public virtual async Task DisposeAsync()
        {
            lock (_locker)
            {
                if (IsDisposeRequested) return;
                IsDisposeRequested = true;
                _deferralEvent.Signal();
            }

            await _deferralEvent.WaitHandle.ToTask().ConfigureAwait(false);
        }

        /// <summary>Suspends disposal until the token that it returns is disposed.
        /// If already disposed, returns null.</summary>
        public IDisposable TryDeferDisposal()
        {
            if (!_deferralEvent.TryAddCount()) return null;
            return Disposable.Create(() => _deferralEvent.Signal());
        }

        private readonly CountdownEvent _deferralEvent = new CountdownEvent(1);
        private readonly object _locker = new object();
    }
}