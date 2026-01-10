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
        public string Sender { get; set; }
        public string EventID { get; set; }
        public PackageType type { get; set; }
        public byte[] Data { get; set; }
        public PackageInfo()
        {
            Data = new byte[0];
            Sender = "";
        }
        public bool FromBytes(byte[] data)
        {
            var eventLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4, sizeof(int)));
            var senderLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8 + eventLength, sizeof(int)));
            int startID = 8;
            EventID = Encoding.UTF8.GetString(data.AsSpan(startID, eventLength));
            startID += eventLength + 4;
            Sender = Encoding.UTF8.GetString(data.AsSpan(startID, senderLength));
            startID += senderLength;

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
            var senderBytes = Encoding.UTF8.GetBytes(Sender);
            var eventIDLenBytes = new byte[4];
            var senderLenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(eventIDLenBytes, eventIDBytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(senderLenBytes, senderBytes.Length);
            var packageLen = 8 + eventIDBytes.Length + Data.Length + 1 + senderBytes.Length;
            if (packageLen > MaxPackageSize)
            {
                throw new ExcessivePackageSizeException($"The package ({packageLen}) exceeds the maximum package size ({MaxPackageSize} bytes)");
            }
            var packageLenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(packageLenBytes, packageLen);
            var result = new byte[packageLen+4];
            int offset = 0;

            Buffer.BlockCopy(packageLenBytes, 0, result, offset, 4);
            offset += 4;

            Buffer.BlockCopy(eventIDLenBytes, 0, result, offset, 4);
            offset += 4;
            Buffer.BlockCopy(eventIDBytes, 0, result, offset, eventIDBytes.Length);
            offset += eventIDBytes.Length;

            Buffer.BlockCopy(senderLenBytes, 0, result, offset, 4);
            offset += 4;
            Buffer.BlockCopy(senderBytes, 0, result, offset, senderBytes.Length);
            offset += senderBytes.Length;

            result[offset] = (byte)type;
            offset += 1;

            Buffer.BlockCopy(Data, 0, result, offset, Data.Length);


            return result;
        }

        public static PackageInfo InvalidPackage => new PackageInfo() { type = PackageType.Invalid };
    }
}
