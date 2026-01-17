using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Models.Data.Capture
{
    public class CaptureFrame : IBinaryConvertable
    {
        public double exposureLength;
        public double gain;

        public bool FromBytes(Span<byte> data)
        {
            exposureLength = BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(0,sizeof(double)));

            gain = BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(sizeof(double)));

            return true;
        }

        public byte[] ToBytes()
        {
            var data = new byte[sizeof(double) * 2];
            BinaryPrimitives.WriteDoubleLittleEndian(data.AsSpan(0,sizeof(double)), exposureLength);
            BinaryPrimitives.WriteDoubleLittleEndian(data.AsSpan(sizeof(double)), gain);

            return data;
        }
    }
}
