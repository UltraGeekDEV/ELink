using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class BinaryConvertableInt32 : IBinaryConvertable
    {
        public int value;
        public BinaryConvertableInt32()
        {
        }

        public bool FromBytes(Span<byte> data)
        {
            if (data.Length == 4)
            {
                value = BinaryPrimitives.ReadInt32LittleEndian(data);
                return true;
            }
            return false;
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(data, value);
            return data;
        }

        public static implicit operator BinaryConvertableInt32(int value) { return new BinaryConvertableInt32() { value = value }; }
    }
}
