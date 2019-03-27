using System;

namespace Furesoft.Signals.Core
{
    internal class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; private set; }

        internal DataReceivedEventArgs(byte[] data, long length)
        {
            if (data != null)
            {
                Data = new byte[length];
                Array.Copy(data, Data, length);
            }
        }
    }
}