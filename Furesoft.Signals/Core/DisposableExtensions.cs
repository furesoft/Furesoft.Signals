using System;
using System.Runtime.CompilerServices;

namespace Furesoft.Signals.Core
{
    public static class IDisposableExtensions
    {
        /// <summary>
        /// Values in a ConditionalWeakTable need to be a reference type,
        /// so box the refcount int in a class.
        /// </summary>
        private class RefCount
        {
            public int refCount;
        }

        private static ConditionalWeakTable<IDisposable, RefCount> refCounts =
                         new ConditionalWeakTable<IDisposable, RefCount>();

        /// <summary>
        /// Extension method for IDisposable.
        /// Increments the refCount for the given IDisposable object.
        /// Note: newly instantiated objects don't automatically have a refCount of 1!
        /// If you wish to use ref-counting, always call retain() whenever you want
        /// to take ownership of an object.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="disposable">The disposable that should be retained.</param>
        public static void Retain(this IDisposable disposable)
        {
            lock (refCounts)
            {
                RefCount refCount = refCounts.GetOrCreateValue(disposable);
                refCount.refCount++;
            }
        }

        /// <summary>
        /// Extension method for IDisposable.
        /// Decrements the refCount for the given disposable.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="disposable">The disposable to release.</param>
        public static void Release(this IDisposable disposable)
        {
            lock (refCounts)
            {
                RefCount refCount = refCounts.GetOrCreateValue(disposable);
                if (refCount.refCount > 0)
                {
                    refCount.refCount--;
                    if (refCount.refCount == 0)
                    {
                        refCounts.Remove(disposable);
                        disposable.Dispose();
                    }
                }
                else
                {
                    // Retain() was never called, so assume there is only
                    // one reference, which is now calling Release()
                    disposable.Dispose();
                }
            }
        }
    }

}