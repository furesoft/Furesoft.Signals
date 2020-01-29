using System;

namespace Furesoft.Signals.Pipe
{
    internal class Disposable : IDisposable
    {
        public static IDisposable Create(Action onDispose) => new Disposable(onDispose);

        public void Dispose()
        {
            Action todo;
            lock (this)
            {
                todo = _onDispose;
                _onDispose = null;
            }
            if (todo != null) todo();
        }

        private Action _onDispose;

        private Disposable(Action onDispose)
        { _onDispose = onDispose; }
    }
}