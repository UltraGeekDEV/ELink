using EVent.Comms;
using EVent.Connections.Models;
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
    public class TCPClientConnection : IConnection
    {
        private string EventID;
        private TcpClient tcpClient;

        private Action<PackageInfo> OnDataRecievedEvent;
        public bool IsAlive { get; private set; }

        public void OnDataRecieved(Action<PackageInfo> handler)
        {
            OnDataRecievedEvent += handler;
        }

        public async Task SendData(PackageInfo package)
        {
            if (!IsAlive)
            {
                return;
            }
            try
            {
                Stream stream = tcpClient.GetStream();
                var packageData = package.ToBytes();
                var messageLen = new byte[sizeof(int)];

                BinaryPrimitives.WriteInt32LittleEndian(messageLen, packageData.Length);

                await stream.WriteAsync(messageLen, 0, messageLen.Length);
                await stream.WriteAsync(packageData, 0, packageData.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while sending data");
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
                    await SendData(handshakePackage);
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
        public static TCPClientConnection? ConnectAsReciever(string EventID, string serverAdress, int serverPort)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { EventID = EventID, tcpClient = tcpClient , IsAlive = true};
            var handshakePackage = new PackageInfo() { type = PackageType.Handshake, EventID = EventID };

            Task.Run(() => connection.RunClient(handshakePackage, serverAdress, serverPort));
            return connection;
        }

        public static TCPClientConnection? ConnectAsTransmitter(string serverAdress, int serverPort)
        {
            var tcpClient = new TcpClient();
            var connection = new TCPClientConnection() { EventID = "null", tcpClient = tcpClient, IsAlive = true };
            var handshakePackage = new PackageInfo() { type = PackageType.Data, EventID = "null" };

            Task.Run(() => connection.RunClient(handshakePackage,serverAdress,serverPort));
            return connection;
        }

        public void Stop()
        {
            IsAlive = false;
        }
    }
}
