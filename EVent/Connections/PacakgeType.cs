using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections
{
    public enum PackageType:byte
    {
        Invalid,
        ConnectEvent,
        Data,
        BroadcastHandshake,
        DisconnectEvent,
        ConnectInterconnect,
        DisconnectInterconnect,
        ConnectFromInterconnect,
        DisconnectFromInterconnect,
        ServerAdminEvent,
    }
}
