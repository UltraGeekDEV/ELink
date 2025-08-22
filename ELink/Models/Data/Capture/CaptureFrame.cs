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

        public IBinaryConvertable FromBytes(byte[] data)
        {
            exposureLength = BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(0,sizeof(double)));

            gain = BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(sizeof(double)));

            return this;
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
