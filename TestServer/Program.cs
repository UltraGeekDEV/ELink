using ELink.Models.Data.Capture;
using ELink.Models.Utils.Comms;
using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.TCP;
using EVent.CoreFunctionality;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var client = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("TestChannel1", "EVentTestServer");

            var clientTask = Task.Run(async () =>
            {
                while (client.IsAlive)
                {
                    Console.WriteLine("PleaseEnterMessage");
                    string? text = Console.ReadLine();
                    if (text != null)
                    {
                        client.SendData(text);
                    }
                }
            });

            while (true) ;
        }
    }
}
