using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.UDP;
using EVent.CoreFunctionality;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPClientConnection
    {
        private QueuedClient client;

        private Action<Package>? OnDataRecievedEvent;
        public bool IsAlive { get; private set; }
        public void OnDataRecieved(Action<Package> handler)
        {
            OnDataRecievedEvent += handler;
        }
        public void UnhookEvent(string eventID)
        {
            var handshakePackage = new Package("EventRemoved", PackageType.ServerAdminEvent, (BinaryConvertableString)eventID);
            SendData(handshakePackage);
        }
        public void HookEvent(string eventID)
        {
            var handshakePackage = new Package("EventAdded",PackageType.ServerAdminEvent,(BinaryConvertableString)eventID);
            SendData(handshakePackage);
        }
        public async void SendData(Package package)
        {
            if (!IsAlive)
            {
                return;
            }

            await client.Send(package);
        }
        private bool RunClient(string serverID)
        {
            var endpoint = ClientTCPDiscovery.GetTCPServer(serverID);
            if (endpoint != null)
            {
                RunClient(endpoint.Address.ToString(),endpoint.Port);
                return true;
            }
            else
            {
                Debug.WriteLine("Couldn't find server or data recieved was corrupted");
                return false;
            }
        }
        private async void RunClient(string serverAdress, int serverPort)
        {
            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverAdress, serverPort);
                client.SetClient(new TCPStreamClient(tcpClient));

                while (IsAlive)
                {
                    var package = await client.ReadPackage();
                    if (package == null)
                    {
                        Debug.WriteLine("Client recieved package was null");
                        IsAlive = false;
                        return;
                    }
                    if (package.type == PackageType.Invalid)
                    {
                        Debug.WriteLine("Client recieved package was invalid");
                        continue;
                    }

                    OnDataRecievedEvent?.Invoke(package);
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine("Server connection forcibly closed");
                IsAlive = false;
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while running client: {ex}");
                await Task.Delay(1000);
            }
        }
        public static TCPClientConnection Connect(string serverAdress, int serverPort)
        {
            var connection = new TCPClientConnection() { client = new QueuedClient(), IsAlive = true };

            Task.Run(() => connection.RunClient(serverAdress,serverPort));
            return connection;
        }
        public static TCPClientConnection Connect(string serverID)
        {
            var connection = new TCPClientConnection() { client = new QueuedClient(), IsAlive = true };

            Task.Run(() => connection.RunClient(serverID));
            return connection;
        }

        public void Stop()
        {
            SendData(new Package("DisconnectClient", PackageType.ServerAdminEvent));
            IsAlive = false;
        }

    }
}
