using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models.BaseBinaryConvertables
{
    public class TCPConnectionData : IBinaryConvertable
    {
        public string TargetIP {  get; set; }
        public int TargetPort { get; set; }
        public int SourcePort { get; set; }
        public bool FromBytes(Span<byte> data)
        {
            try
            {
                string combined = Encoding.UTF8.GetString(data);
                var split = combined.Split(':');
                TargetIP = split[0];
                TargetPort = int.Parse(split[1]);
                SourcePort = int.Parse(split[2]);
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
                return Encoding.UTF8.GetBytes($"{TargetIP}:{TargetPort}:{SourcePort}");
            }
            catch
            {
                return new byte[0];
            }
        }
    }
}
