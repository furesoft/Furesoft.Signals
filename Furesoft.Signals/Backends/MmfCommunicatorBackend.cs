using Furesoft.Signals.Core;
using System;

namespace Furesoft.Signals.Backends
{
    public class MmfCommunicatorBackend : ISignalBackend
    {
        public long Capacity { get; private set; }

        public string Name { get; private set; }

        public MmfCommunicatorBackend(string name, long capacity)
        {
            Name = name;
            Capacity = capacity;
        }

        public event Action<byte[]> OnNewMessage;

        public void Initialize(bool isOwner)
        {
            if (isOwner)
            {
                communicator = new MemoryMappedFileCommunicator(Name, Capacity);
                communicator.ReadPosition = 2000;
                communicator.WritePosition = 0;
                communicator.StartReader();
            }
            else
            {
                communicator = new MemoryMappedFileCommunicator(Name, Capacity);
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