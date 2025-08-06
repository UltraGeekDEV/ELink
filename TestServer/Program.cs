using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.TCP;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TCPServer tcpServer = new TCPServer();
            tcpServer.Run(IPAddress.Any, 5000);

            var clientTask = Task.Run(() =>
            {
                var transmitter = TCPClientConnection.ConnectAsTransmitter("127.0.0.1", 5000);

                while (transmitter.IsAlive)
                {
                    var package = new PackageInfo() { type = PackageType.Data };

                    Console.WriteLine("Enter event selector");
                    var eventID = Console.ReadLine();
                    Console.WriteLine("Enter message");
                    var message = Console.ReadLine();

                    package.EventID = eventID;
                    package.Data = Encoding.UTF8.GetBytes(message);

                    transmitter.SendData(package).Wait();
                }
            });

            while (true) ;
        }
    }
}
