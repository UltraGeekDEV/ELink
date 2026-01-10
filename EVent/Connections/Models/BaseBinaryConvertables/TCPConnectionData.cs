using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class TCPConnectionData : IBinaryConvertable
    {
        public string IP {  get; set; }
        public int Port { get; set; }
        public bool FromBytes(byte[] data)
        {
            try
            {
                string combined = Encoding.UTF8.GetString(data);
                var split = combined.Split(':');
                IP = split[0];
                Port = int.Parse(split[1]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] ToBytes()
        {
            try
            {
                return Encoding.UTF8.GetBytes($"{IP}:{Port}");
            }
            catch
            {
                return new byte[0];
            }
        }
    }
}
