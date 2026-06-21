using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class Package : IBinaryConvertable
    {
        public static uint MaxPackageSize = 202 * 1024 * 1024; // ~100Mp 16 bit mono+ 2Mb overhead
        public string EventID { get; set; }
        public PackageType type { get; set; }
        public byte[] Data { get; set; }
        public Package()
        {
            EventID = string.Empty;
            Data = new byte[0];
        }
        public Package(string EventID, PackageType type, IBinaryConvertable payload)
        {
            this.EventID = EventID;
            this.type = type;
            Data = payload.ToBytes();
        }
        public Package(string EventID, PackageType type)
        {
            this.EventID = EventID;
            this.type = type;
            Data = new byte[0];
        }
        public bool FromBytes(Span<byte> data)
        {
            int offset = 4;
            var eventLength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(int)));
            offset += 4;
            
            EventID = Encoding.UTF8.GetString(data.Slice(offset, eventLength));
            offset += eventLength;

            type = (PackageType)data[offset];
            offset++;
            Data = data.Slice(offset).ToArray();
            return true;
        }
        public static async Task<Package?> ReadPackage(Stream stream)
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

                int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);

                if (payloadLength < 0 || payloadLength > MaxPackageSize - 4)
                    return null;

                int messageLength = payloadLength + 4;

                var receivedData = new byte[messageLength];
                receivedData[0] = buffer[0];
                receivedData[1] = buffer[1];
                receivedData[2] = buffer[2];
                receivedData[3] = buffer[3];
                totalRead += 4;

                while (totalRead < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(receivedData, totalRead, messageLength - totalRead);
                    if (bytesRead == 0)
                        return null;
                    totalRead += bytesRead;
                }

                if (receivedData.Length != messageLength)
                {
                    Debug.WriteLine($"Message degenerate, received/expected: {totalRead} / {messageLength}");
                    return null;
                }
                var ret = new Package();
                bool sucess = ret.FromBytes(receivedData);
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
            var tempByteArr = new byte[4];

            var packageLen = 4 + 1 + eventIDBytes.Length + Data.Length; //EventID length, Type, EventID, Data
            if (packageLen > MaxPackageSize)
            {
                throw new ExcessivePackageSizeException($"The package ({packageLen}) exceeds the maximum package size ({MaxPackageSize} bytes)");
            }
            var result = new byte[packageLen + 4];
            int offset = 0;

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, packageLen);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, eventIDBytes.Length);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            Buffer.BlockCopy(eventIDBytes, 0, result, offset, eventIDBytes.Length);
            offset += eventIDBytes.Length;

            result[offset] = (byte)type;
            offset += 1;

            if (Data.Length > 0)
            {
                Buffer.BlockCopy(Data, 0, result, offset, Data.Length);
            }

            return result;
        }

        public static Package InvalidPackage => new Package() { type = PackageType.Invalid };
    }
}
