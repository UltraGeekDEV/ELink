using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class PackageInfo : IBinaryConvertable
    {
        public static uint MaxPackageSize = 202 * 1024 * 1024; // ~100Mp 16 bit mono+ 2Mb overhead
        public string EventID { get; set; }
        public PackageType type { get; set; }
        public byte[] Data { get; set; }
        public PackageInfo()
        {
            Data = new byte[0];
        }
        public IBinaryConvertable FromBytes(byte[] data)
        {
            var stringLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, sizeof(int)));
            int startID = sizeof(int);
            EventID = Encoding.UTF8.GetString(data.AsSpan(startID, stringLength));
            startID += stringLength;

            type = (PackageType)data[startID];
            startID++;
            Data = data.AsSpan(startID).ToArray();
            return this;
        }
        public static async Task<PackageInfo?> ReadPackage(Stream stream)
        {
            try
            {
                byte[] buffer = new byte[4096];
                int totalRead = 0;
                while (totalRead < sizeof(int))
                {
                    int bytesRead = await stream.ReadAsync(buffer, totalRead, sizeof(int) - totalRead);
                    if (bytesRead == 0)
                        return null;
                    totalRead += bytesRead;
                }
                totalRead = 0;

                var messageLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);

                if (messageLength > MaxPackageSize) return null;

                var recievedData = new byte[messageLength];

                while (totalRead < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(recievedData, totalRead, messageLength - totalRead);
                    if (bytesRead == 0)
                        return null;
                    totalRead += bytesRead;
                }

                if (recievedData.Length != messageLength)
                {
                    Debug.WriteLine($"Message degenerate, recieved/expected: {totalRead} / {messageLength}");
                    return InvalidPackage;
                }
                var ret = new PackageInfo();
                return (PackageInfo)ret.FromBytes(recievedData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while reading message: {ex.Message}");
                return null;
            }
        }
        public byte[] ToBytes()
        {
            var eventIDBytes = Encoding.UTF8.GetBytes(EventID);
            var lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, eventIDBytes.Length);
            var packageLen = 4 + eventIDBytes.Length + Data.Length + 1;
            if (packageLen > MaxPackageSize)
            {
                throw new ExcessivePackageSizeException($"The package ({packageLen}) exceeds the maximum package size ({MaxPackageSize} bytes)");
            }

            var result = new byte[packageLen];
            Buffer.BlockCopy(lengthBytes, 0, result, 0, 4);
            Buffer.BlockCopy(eventIDBytes, 0, result, 4, eventIDBytes.Length);
            Buffer.BlockCopy(new byte[] { (byte)type }, 0, result, 4 + eventIDBytes.Length, 1);
            Buffer.BlockCopy(Data, 0, result, 1 + 4 + eventIDBytes.Length, Data.Length);

            return result;
        }

        public static PackageInfo InvalidPackage => new PackageInfo() { type = PackageType.Invalid };
    }
}
