using System;
using System.IO;

namespace Furesoft.Signals.Messages
{
    public class StreamChunk
    {
        public byte[] Data { get; set; }
        public int ID { get; set; }
        public int Length { get; set; }
    }
}