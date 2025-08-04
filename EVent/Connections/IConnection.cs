using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVent.Comms;
using EVent.Connections.TCP;

namespace EVent.Connections
{
    public interface IConnection
    {
        public void SendData(PackageInfo data);
        public void ExpectData(Action<PackageInfo> handler);
    }
}
