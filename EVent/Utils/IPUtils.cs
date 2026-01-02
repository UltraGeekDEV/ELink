using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Utils
{
    public static class IPUtils
    {
        public static IPAddress GetLocalIPv4()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530); // no traffic is sent
            return ((IPEndPoint)socket.LocalEndPoint!).Address;
        }
    }
}
