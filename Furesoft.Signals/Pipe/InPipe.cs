using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Furesoft.Signals.Pipe
{
    internal class InPipe : MutexFreePipe
    {
        public InPipe(string name, bool createBuffer, Action<byte[]> onMessage) : base(name, createBuffer)
        {
            _onMessage = onMessage;
            new Thread(Go).Start();
        }

        public override async Task DisposeAsync()
        {
            NewMessageSignal.Set();
            await base.DisposeAsync().ConfigureAwait(false);
        }

        private readonly Action<byte[]> _onMessage;
        private int _bufferCount;
        private int _lastMessageProcessed;

        private unsafe int? GetLatestMessageID()
        {
            using (var token = Buffer.TryDeferDisposal())
                return token == null ? (int?)null : *((int*)Buffer.Pointer);
        }

        private unsafe byte[] GetNextMessage()
        {
            _lastMessageProcessed++;

            using (var df = TryDeferDisposal())
            {
                if (df == null) return null;

                using (var bufToken = Buffer.DeferDisposal())
                {
                    if (bufToken == null) return null;

                    byte* offsetPointer = Buffer.Pointer + Offset;
                    var msgPointer = (int*)offsetPointer;

                    int msgLength = *msgPointer;

                    Offset += MessageHeaderLength;
                    offsetPointer += MessageHeaderLength;

                    if (msgLength == 0)
                    {
                        Buffer.Accessor.Write(4, true);   // Signal that we no longer need file
                        Buffer.Dispose();
                        string newName = Name + "." + ++_bufferCount;
                        if (IsDisposeRequested) return null;
                        Buffer = new SafeMemoryMappedFile(MemoryMappedFile.OpenExisting(newName));
                        Offset = StartingOffset;
                        return new byte[0];
                    }

                    Offset += msgLength;

                    //MMF.Accessor.ReadArray (Offset, msg, 0, msg.Length);    // too slow
                    var msg = new byte[msgLength];
                    Marshal.Copy(new IntPtr(offsetPointer), msg, 0, msg.Length);
                    return msg;
                }
            }
        }

        private void Go()
        {
            int spinCycles = 0;
            while (true)
            {
                int? latestMessageID = GetLatestMessageID();
                if (latestMessageID == null) return;            // We've been disposed.

                if (latestMessageID > _lastMessageProcessed)
                {
                    Thread.MemoryBarrier();    // We need this because of lock-free implementation
                    byte[] msg = GetNextMessage();
                    if (msg == null || IsDisposeRequested) return;
                    if (msg.Length > 0 && _onMessage != null) _onMessage(msg);       // Zero-length msg will be a buffer continuation
                    spinCycles = 1000;
                }
                if (spinCycles == 0)
                {
                    NewMessageSignal.WaitOne();
                    if (IsDisposeRequested) return;
                }
                else
                {
                    Thread.MemoryBarrier();    // We need this because of lock-free implementation
                }
            }
        }
    }
}