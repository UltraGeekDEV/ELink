using EVent.Comms;
using EVent.Connections.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : IConnection
    {
        Action<PackageInfo>? DataRecieved;
        TcpListener tcpListener;
        Task mainThread;

        Dictionary<string, List<TcpClient>> events;
        //Solve single channel multi client issues
        public void Run()
        {
            mainThread = Task.Run(() =>
            {
                tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
                tcpListener.Start();
                while (true)
                {
                    var client = tcpListener.AcceptTcpClient();
                    AcceptClient(client);
                }
            });   
        }
        private async Task AcceptClient(TcpClient client)
        {
            var stream = client.GetStream();

            var package = await ReadPackage(stream);

            client.Close();
        }
        private async Task<PackageInfo?> ReadPackage(Stream stream)
        {
            byte[] buffer = new byte[4096];
            int totalRead = 0;
            while (totalRead < sizeof(int))
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, sizeof(int) - totalRead);
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }
            totalRead = 0;

            var messageLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);

            if(messageLength > PackageInfo.MaxPackageSize) return null;

            var recievedData = new byte[messageLength];

            while (totalRead < messageLength)
            {
                int bytesRead = await stream.ReadAsync(recievedData, totalRead, messageLength - totalRead);
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }

            if( recievedData.Length != messageLength ) return null;

           return new PackageInfo(recievedData);
        }
        
        public void SendData(PackageInfo package)
        {
           
        }

        public void ExpectData(Action<PackageInfo> handler)
        {
            DataRecieved += handler;
        }
    }
}
