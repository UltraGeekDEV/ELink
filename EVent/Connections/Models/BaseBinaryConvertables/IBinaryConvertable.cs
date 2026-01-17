using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public interface IBinaryConvertable
    {
        public bool FromBytes(Span<byte> data);
        public byte[] ToBytes();
    }
}
