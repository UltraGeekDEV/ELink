using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models
{
    public interface IBroadcastServer
    {
        public void SetupConenction();
        public void ReceivePacket();
    }
}
