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
    public class ServerTCPBroadcast
    {
        public static int EVentTCPConenctionBroadcastPort = 8563;
        public static string EVentBroadcastGroup = "239.255.12.85";

        private IPAddress broadcastGroup;
        private Task mainThread;

        private string serverID;
        private string ip;
        private string port;

        public ServerTCPBroadcast(string serverID, string ip, string port)
        {
            this.serverID = serverID;
            this.ip = ip;
            this.port = port;
            broadcastGroup = IPAddress.Parse(EVentBroadcastGroup);
        }

        public void Start()
        {
            mainThread = Task.Run(() =>
            {
                UdpClient udpClient = new UdpClient();
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, EVentTCPConenctionBroadcastPort));
                udpClient.JoinMulticastGroup(broadcastGroup);

                while (true)
                {
                    try
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, EVentTCPConenctionBroadcastPort);
                        byte[] data = udpClient.Receive(ref remoteEndPoint);
                        PackageInfo info = new PackageInfo();
                        bool sucesfull = info.FromBytes(data);

                        if (sucesfull && info.type == PackageType.BroadcastHandshake)
                        {
                            if (info.EventID.Equals(serverID))
                            {
                                Task.Run(()=> SendRepply(remoteEndPoint));
                                Debug.WriteLine("Client Wants to connect");
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            });
        }

        private void SendRepply(IPEndPoint remoteEndpoint)
        {
            using (UdpClient sender = new UdpClient())
            {
                PackageInfo package = new PackageInfo();
                package.type = PackageType.BroadcastHandshake;
                package.EventID = serverID + "Callback";
                package.Data = new BroadcastHandshake(ip, port).ToBytes();

                sender.Send(package.ToBytes(), remoteEndpoint);
            }
        }
    }
}
