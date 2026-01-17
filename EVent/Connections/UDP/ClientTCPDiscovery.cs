using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.UDP
{
    public class ClientTCPDiscovery
    {
        public static IPEndPoint? GetTCPServer(string ServerID)
        {
            UdpClient udpClient = new UdpClient();
            while (true)
            {
                var endpoint = new IPEndPoint(IPAddress.Any, ServerTCPBroadcast.EVentTCPConenctionBroadcastPort);
                try
                {
                    udpClient.Send(new Package() { type = PackageType.BroadcastHandshake, EventID = ServerID, Data = new byte[0] }.ToBytes()
                                        , new IPEndPoint(IPAddress.Parse(ServerTCPBroadcast.EVentBroadcastGroup)
                                        , ServerTCPBroadcast.EVentTCPConenctionBroadcastPort));
                }
                catch
                {
                    Debug.WriteLine("Failed to send network discovery pacekt");
                }
                try
                {
                    var bytes = udpClient.Receive(ref endpoint);

                    Package info = new Package();
                    bool sucesfullRead = info.FromBytes(bytes);

                    BroadcastHandshake serverData = new BroadcastHandshake();
                    sucesfullRead &= serverData.FromBytes(info.Data);

                    if (sucesfullRead)
                    {
                        var ip = new IPAddress(serverData.IP);
                        int port = serverData.Port[0] << 8 | serverData.Port[1];
                        return new IPEndPoint(ip, port);
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to recieve server info for {ServerID}");
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception when conencting to {ServerID} : {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }
    }
}
