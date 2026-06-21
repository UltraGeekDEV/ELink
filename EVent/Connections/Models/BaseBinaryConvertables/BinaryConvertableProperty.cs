using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    internal class BinaryConvertableProperty : IBinaryConvertable
    {
        public bool FromBytes(Span<byte> data)
        {
            throw new NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new NotImplementedException();
        }
    }
}
