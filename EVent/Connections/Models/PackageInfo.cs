using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models
{
    public class PackageInfo
    {
        public static uint MaxPackageSize = 202 * 1024 * 1024; // ~100Mp 16 bit mono+ 2Mb overhead
        public string EventID { get; set; }
        public PackageType type { get; set; }
        public byte[] Data { get; set; }
        public PackageInfo()
        {
        }
        public PackageInfo(byte[] data)
        {
            var stringLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, sizeof(uint)));
            int startID = sizeof(uint);

            EventID = Encoding.UTF8.GetString(data.AsSpan(startID, stringLength));

            startID += stringLength;
            Data = data.AsSpan(startID).ToArray();
        }

        public byte[] ToBytes()
        {
            var eventIDBytes = Encoding.UTF8.GetBytes(EventID);
            var lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, eventIDBytes.Length);
            var packageLen = 4 + eventIDBytes.Length + Data.Length;
            if (packageLen > MaxPackageSize)
            {
                throw new ExsessivePackageSizeException($"The package ({packageLen}) exceeds the maximum package size ({MaxPackageSize} bytes)");
            }

            var result = new byte[packageLen];
            Buffer.BlockCopy(lengthBytes, 0, result, 0, 4);
            Buffer.BlockCopy(eventIDBytes, 0, result, 4, eventIDBytes.Length);
            Buffer.BlockCopy(Data, 0, result, 4 + eventIDBytes.Length, Data.Length);

            return result;
        }

    }
}
