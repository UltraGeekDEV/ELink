using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class BinaryConvertableFloat : IBinaryConvertable
    {

        public float value;
        public BinaryConvertableFloat()
        {
        }

        public bool FromBytes(Span<byte> data)
        {
            if (data.Length == 4)
            {
                value = BinaryPrimitives.ReadSingleLittleEndian(data);
                return true;
            }
            return false;
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(data, value);
            return data;
        }

        public static implicit operator BinaryConvertableFloat(float value) { return new BinaryConvertableFloat() { value = value }; }
    }
}
