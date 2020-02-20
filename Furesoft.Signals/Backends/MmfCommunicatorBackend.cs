using Furesoft.Signals.Core;
using System;

namespace Furesoft.Signals.Backends
{
    public class MmfCommunicatorBackend : ISignalBackend
    {
        public event Action<byte[]> OnNewMessage;

        public void Dispose()
        {
            communicator.Dispose();
        }

        public void Initialize(string name, long capacity, bool isOwner)
        {
            if (isOwner)
            {
                communicator = new MemoryMappedFileCommunicator(name, capacity);
                communicator.ReadPosition = 2000;
                communicator.WritePosition = 0;
                communicator.StartReader();
            }
            else
            {
                communicator = new MemoryMappedFileCommunicator(name, capacity);
                communicator.WritePosition = 2000;
                communicator.ReadPosition = 0;
                communicator.StartReader();
            }

            communicator.DataReceived += Communicator_DataReceived;
        }

        public void Write(byte[] data)
        {
            communicator.Write(data);
        }

        internal MemoryMappedFileCommunicator communicator;

        private void Communicator_DataReceived(object sender, DataReceivedEventArgs e)
        {
            OnNewMessage?.Invoke(e.Data);
        }
    }
}