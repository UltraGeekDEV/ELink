using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    internal class TCPStreamClient : IStreamClient
    {
        private TcpClient tcpClient;
        private Stream stream;

        public TCPStreamClient(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            stream = tcpClient.GetStream();
        }
        public static implicit operator TCPStreamClient(TcpClient client)
        {
            return new TCPStreamClient(client);
        }

        public async Task<Package?> ReadPackage()
        {
            return await Package.ReadPackage(stream);
        }

        public async Task Send(byte[] data)
        {
            await stream.WriteAsync(data);
        }
        public void Close()
        {
            stream.Close();
        }
    }
}
