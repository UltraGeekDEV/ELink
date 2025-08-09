using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class BinaryConvertableString : IBinaryConvertable
    {
        public string Text { get; set; }
        public IBinaryConvertable FromBytes(byte[] data)
        {
            Text = Encoding.UTF8.GetString(data);
            return this;
        }

        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(Text);
        }
        public override string ToString()
        {
            return this;
        }

        public static implicit operator string(BinaryConvertableString a)
        {
            return a.Text;
        }
        public static implicit operator BinaryConvertableString(string a)
        {
            return new BinaryConvertableString() { Text = a};
        }
    }
}
