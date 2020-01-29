using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    internal class SafeMemoryMappedFile : AsyncDisposable
    {
        public MemoryMappedViewAccessor Accessor
        {
            get { AssertSafe(); return _accessor; }
        }

        public int Length { get; private set; }

        public unsafe byte* Pointer
        {
            get { AssertSafe(); return _pointer; }
        }

        public unsafe SafeMemoryMappedFile(MemoryMappedFile mmFile)
        {
            _mmFile = mmFile;
            _accessor = _mmFile.CreateViewAccessor();
            _pointer = (byte*)_accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            Length = (int)_accessor.Capacity;
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            _accessor.Dispose();
            _mmFile.Dispose();
        }

        private readonly MemoryMappedViewAccessor _accessor;
        private readonly MemoryMappedFile _mmFile;
        private readonly unsafe byte* _pointer;
    }
}