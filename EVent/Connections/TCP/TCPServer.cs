using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.UDP;
using EVent.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : ICommsProtocol
    {
        TcpListener tcpListener;
        List<ServerTCPBroadcast> discoveryBroadcastChannels;
        bool IsAlive = true;

        IPAddress listeningAdress;
        int port;

        Action<QueuedClient>? onClientAccepted;

        public TCPServer(IPAddress listeningAdress, int port)
        {
            this.listeningAdress = listeningAdress;
            this.port = port;
        }
        public TCPServer(IPAddress listeningAdress, int port,params string[] serverBroadcastedIDs) : this(listeningAdress, port)
        {
            IPAddress localIP = IPUtils.GetLocalIPv4();

            discoveryBroadcastChannels = serverBroadcastedIDs.Select(x =>
            {
                var broadcast = new ServerTCPBroadcast(x, localIP.ToString(), port.ToString());
                broadcast.Start();
                return broadcast;
            }).ToList();
        }
        public void Run()
        {
            Debug.WriteLine("Server started");
            
            Task.Run(async () =>
            {
                try
                {
                    tcpListener = new TcpListener(listeningAdress, port);
                    tcpListener.Start();
                    while (IsAlive)
                    {
                        AcceptClient(tcpListener.AcceptTcpClient());
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception on the server{ex.Message}");
                }
            });   
        }
        private void AcceptClient(TcpClient tcpClient)
        {
            QueuedClient client = new QueuedClient();
            client.SetClient(new TCPStreamClient(tcpClient));
            onClientAccepted?.Invoke(client);
        }

        public void Stop()
        {
            IsAlive = false;
        }

        public void OnClientAccepted(Action<QueuedClient> action)
        {
            onClientAccepted += action;
        }

        public async Task<QueuedClient?> EstablishInterconnect(Package package)
        {
            TcpClient tcpClient = new TcpClient();
            TCPConnectionData connectionData = new TCPConnectionData();
            if (connectionData.FromBytes(package.Data))
            {
                try
                {
                    tcpClient.Connect(connectionData.IP, connectionData.Port);
                    var client = new QueuedClient(new TCPStreamClient(tcpClient));

                    await client.Send(new Package("UpgradeToInterconnect",PackageType.ServerAdminEvent));

                    return client;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
