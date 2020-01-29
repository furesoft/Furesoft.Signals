using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    internal abstract class MutexFreePipe : AsyncDisposable
    {
        public readonly string Name;

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await Buffer.DisposeAsync().ConfigureAwait(false);
            NewMessageSignal.Dispose();
        }

        protected const int MinimumBufferSize = 0x10000;
        protected readonly int MessageHeaderLength = sizeof(int);
        protected readonly EventWaitHandle NewMessageSignal;
        protected readonly int StartingOffset = sizeof(int) + sizeof(bool);
        protected SafeMemoryMappedFile Buffer;
        protected int Offset, Length;

        protected MutexFreePipe(string name, bool createBuffer)
        {
            Name = name;

            var mmFile = createBuffer
                ? MemoryMappedFile.CreateNew(name + ".0", MinimumBufferSize, MemoryMappedFileAccess.ReadWrite)
                : MemoryMappedFile.OpenExisting(name + ".0");

            Buffer = new SafeMemoryMappedFile(mmFile);
            NewMessageSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".signal");

            Length = Buffer.Length;
            Offset = StartingOffset;
        }
    }
}