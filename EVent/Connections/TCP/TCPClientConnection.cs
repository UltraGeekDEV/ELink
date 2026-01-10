using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.UDP;
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
        private TcpClient tcpClient;

        private Action<PackageInfo> OnDataRecievedEvent;
        public bool IsAlive { get; private set; }
        private object sendLock = new object();
        public void OnDataRecieved(Action<PackageInfo> handler)
        {
            OnDataRecievedEvent += handler;
        }
        public void UnhookEvent(string eventID)
        {
            var handshakePackage = new PackageInfo() { type = PackageType.DisconnectEvent, EventID = eventID };
            SendData(handshakePackage);
        }
        public void HookEvent(string eventID)
        {
            var handshakePackage = new PackageInfo() { type = PackageType.ConnectEvent, EventID = eventID };
            SendData(handshakePackage);
        }
        public void SendData(PackageInfo package)
        {
            if (!IsAlive)
            {
                return;
            }
            try
            {
                while(!tcpClient.Connected) { }
                lock (sendLock)
                {
                    Stream stream = tcpClient.GetStream();
                    var packageData = package.ToBytes();
                    stream.Write(packageData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while sending data");
            }
        }
        private bool RunClient(PackageInfo handshakePackage, string serverID)
        {
            var endpoint = ClientTCPDiscovery.GetTCPServer(serverID);
            if (endpoint != null)
            {
                RunClient(handshakePackage,endpoint.Address.ToString(),endpoint.Port);
                return true;
            }
            else
            {
                Debug.WriteLine("Couldn't find server or data recieved was corrupted");
                return false;
            }
        }
        private async void RunClient(PackageInfo handshakePackage, string serverAdress, int serverPort)
        {
            while(IsAlive)
            {
                try
                {
                    await tcpClient.ConnectAsync(serverAdress, serverPort);
                    var stream = tcpClient.GetStream();
                    SendData(handshakePackage);
                    while (IsAlive)
                    {
                        var package = await PackageInfo.ReadPackage(stream);
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
                catch(IOException ioEx)
                {
                    Debug.WriteLine("Server connection forcibly closed");
                    IsAlive = false;
                    return;
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error while running client: {ex}");
                    await Task.Delay(1000);
                }
            }

        }
        public static TCPClientConnection ConnectAsReciever(string EventID, string serverAdress, int serverPort)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { tcpClient = tcpClient , IsAlive = true};
            var handshakePackage = new PackageInfo() { type = PackageType.ConnectEvent, EventID = EventID };

            Task.Run(() => connection.RunClient(handshakePackage, serverAdress, serverPort));
            return connection;
        }
        public static TCPClientConnection ConnectAsReciever(string EventID, string serverID)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { tcpClient = tcpClient, IsAlive = true };
            var handshakePackage = new PackageInfo() { type = PackageType.ConnectEvent, EventID = EventID };

            Task.Run(() => connection.RunClient(handshakePackage, serverID));
            return connection;
        }

        public static TCPClientConnection ConnectAsTransmitter(string serverAdress, int serverPort)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { tcpClient = tcpClient, IsAlive = true };
            var handshakePackage = new PackageInfo() { type = PackageType.Data, EventID = "null" };

            Task.Run(() => connection.RunClient(handshakePackage,serverAdress,serverPort));
            return connection;
        }
        public static TCPClientConnection ConnectAsTransmitter(string serverID)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { tcpClient = tcpClient, IsAlive = true };
            var handshakePackage = new PackageInfo() { type = PackageType.Data, EventID = "null" };

            Task.Run(() => connection.RunClient(handshakePackage, serverID));
            return connection;
        }

        public void Stop()
        {
            IsAlive = false;
        }

    }
}
