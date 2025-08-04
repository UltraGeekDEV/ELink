using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVent.Comms;

namespace EVent.Connections.Models
{
    public interface IConnection
    {
        public void SendData(PackageInfo data);
        public void ExpectData(Action<PackageInfo> handler);
    }
}
