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
        public bool FromBytes(byte[] data)
        {
            var stringLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, sizeof(int)));
            int startID = 8;
            EventID = Encoding.UTF8.GetString(data.AsSpan(startID, stringLength));
            startID += stringLength;

            type = (PackageType)data[startID];
            startID++;
            Data = data.AsSpan(startID).ToArray();
            return true;
        }
        public static async Task<PackageInfo?> ReadPackage(Stream stream)
        {
            try
            {
                byte[] buffer = new byte[sizeof(int)];
                int totalRead = 0;
                while (totalRead < sizeof(int))
                {
                    int bytesRead = await stream.ReadAsync(buffer, totalRead, sizeof(int) - totalRead);
                    if (bytesRead == 0)
                        return null;
                    totalRead += bytesRead;
                }
                totalRead = 0;

                var messageLength = BinaryPrimitives.ReadInt32LittleEndian(buffer) + 4;

                if (messageLength > MaxPackageSize) return null;

                var recievedData = new byte[messageLength];
                recievedData[0] = buffer[0];
                recievedData[1] = buffer[1];
                recievedData[2] = buffer[2];
                recievedData[3] = buffer[3];
                totalRead += 4;

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
                bool sucess = ret.FromBytes(recievedData);
                if (sucess)
                {
                    return ret;
                }
                else
                {
                    return null;
                }
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
            var packageLenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(packageLenBytes, packageLen);
            var result = new byte[packageLen+4];
            Buffer.BlockCopy(packageLenBytes, 0, result, 0, 4);
            Buffer.BlockCopy(lengthBytes, 0, result, 4, 4);
            Buffer.BlockCopy(eventIDBytes, 0, result, 8, eventIDBytes.Length);
            result[8 + eventIDBytes.Length] = (byte)type;
            Buffer.BlockCopy(Data, 0, result, 1 + 8 + eventIDBytes.Length, Data.Length);

            return result;
        }

        public static PackageInfo InvalidPackage => new PackageInfo() { type = PackageType.Invalid };
    }
}
