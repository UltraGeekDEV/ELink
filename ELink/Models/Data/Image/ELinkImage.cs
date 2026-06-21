using ELink.Interfaces.Utils;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ELink.Models.Data.Image
{
    public class ELinkImage : IBinaryConvertable
    {
        public Dictionary<string, FITSHeaderItem> FitsHeader { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public ImageType Type { get; set; }
        public float[] Data { get; set; }
        public string Filter { get; set; }
        public ELinkImage()
        {
            FitsHeader = new Dictionary<string, FITSHeaderItem>();
            Data = new float[0];
        }

        public bool FromBytes(Span<byte> data)
        {
            int offset = 0;

            Width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;

            Height = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;

            Type = (ImageType)BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;

            int headerCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;

            for (int i = 0; i < headerCount; i++)
            {
                int length = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
                offset += 4;

                FITSHeaderItem item = new FITSHeaderItem();
                item.FromBytes(data.Slice(offset, length));
                FitsHeader.Add( item.key, item);
                offset += length;
            }

            int imageSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
            offset += 4;

            Data = new float[imageSize];

            for (int i = 0; i < imageSize; i++)
            {
                Data[i] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4));
                offset += sizeof(int);
            }

            Filter = Encoding.UTF8.GetString(data.Slice(offset));

            return true;
        }

        public byte[] ToBytes()
        {
            int offset = 0;
            var tempByteArr = new byte[4];

            var header = FitsHeader.Select(x=>x.Value.ToBytes()).ToArray();
            var filterBytes = Encoding.UTF8.GetBytes(Filter);

            byte[] result = new byte[filterBytes.Length + header.Length * 4 + header.Sum(x=>x.Length) + 5 * 4 + Data.Length * 4];

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, Width);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, Height);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, (int)Type);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, header.Length);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            foreach( var item in header)
            {
                BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, item.Length);
                Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
                offset += 4;

                Buffer.BlockCopy(item, 0, result, offset, item.Length);
                offset += item.Length;
            }

            BinaryPrimitives.WriteInt32LittleEndian(tempByteArr, Data.Length);
            Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
            offset += 4;

            for (int i = 0; i < Data.Length; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(tempByteArr, Data[i]);
                Buffer.BlockCopy(tempByteArr, 0, result, offset, 4);
                offset += 4;
            }

            Buffer.BlockCopy(filterBytes, 0, result, offset, filterBytes.Length);

            return result;
        }
    }
}
