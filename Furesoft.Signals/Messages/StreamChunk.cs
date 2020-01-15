using System;
using System.IO;

namespace Furesoft.Signals.Messages
{
    public class StreamChunk
    {
        public byte[] Data { get; set; }
        public int ID { get; set; }
        public int Length { get; set; }

        public static StreamChunk Deserialize(byte[] raw)
        {
            var br = new BinaryReader(new MemoryStream(raw));

            var magic = br.ReadInt32();
            if (magic == Magic)
            {
                var res = new StreamChunk();
                res.ID = br.ReadInt32();
                res.Length = br.ReadInt32();
                res.Data = Convert.FromBase64String(br.ReadString());

                return res;
            }

            br.Close();

            return null;
        }

        public byte[] Serialize()
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            bw.Write(Magic);
            bw.Write(ID);
            bw.Write(Length);
            bw.Write(Convert.ToBase64String(Data));

            return ms.ToArray();
        }

        private const int Magic = 0x333;
    }
}