using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Force.Crc32;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    internal class BroadcastHandshake : IBinaryConvertable
    {
        public byte[] IP { get; set; }
        public byte[] Port { get; set; }

        public BroadcastHandshake()
        {
            
        }
        public BroadcastHandshake(string ip, string port)
        {
            IP = new byte[4];
            Port = new byte[2];
            var splitParts = ip.Split('.');

            IP[0] = byte.Parse(splitParts[0]);
            IP[1] = byte.Parse(splitParts[1]);
            IP[2] = byte.Parse(splitParts[2]);
            IP[3] = byte.Parse(splitParts[3]);

            ushort portNum = ushort.Parse(port);
            Port[0] = (byte)(portNum >> 8);
            Port[1] = (byte)(portNum & 0xFF); 
        }
        public bool FromBytes(Span<byte> data)
        {
            uint crcCheck = Crc32Algorithm.Compute(data.ToArray(), 0, 6);
            uint crc = (uint)(data[6] | data[7] << 8 | data[8] << 16 | data[9] << 24);
            if (crcCheck != crc)
            {
                return false;
            }

            IP = new byte[4];
            Port = new byte[2];

            IP[0] = data[0];
            IP[1] = data[1];
            IP[2] = data[2];
            IP[3] = data[3];

            Port[0] = data[4];
            Port[1] = data[5];

            return true;
        }

        public byte[] ToBytes()
        {
            var data = new byte[10];
            data[0] = IP[0] ;
            data[1] = IP[1] ;
            data[2] = IP[2] ;
            data[3] = IP[3] ;

            data[4] = Port[0];
            data[5] = Port[1];

            uint crc = Crc32Algorithm.Compute(data, 0, 6);

            data[6] = (byte)(crc & 0xFF);
            data[7] = (byte)((crc >> 8) & 0xFF);
            data[8] = (byte)((crc >> 16) & 0xFF);
            data[9] = (byte)((crc >> 24) & 0xFF);

            return data;
        }
    }
}
