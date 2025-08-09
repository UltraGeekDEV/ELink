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
            var servers = new List<IServer>() { new TCPServer(IPAddress.Any, 5000) };
            var eVentHub = new EventHub(servers);

            eVentHub.Setup();

            var transmitterText = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("A", "127.0.0.1", 5000);
            var transmitterDate = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("A|B", "127.0.0.1", 5000);

            var clientTask = Task.Run(() =>
            {
                while (transmitterText.IsAlive)
                {
                    Console.WriteLine("Enter message");
                    var message = Console.ReadLine();

                    if (message != null)
                    {
                        transmitterText.SendData(message).Wait();
                        Console.WriteLine($"Time is:{DateTime.Now.Hour}:{DateTime.Now.Minute}.{DateTime.Now.Second}.{DateTime.Now.Millisecond}");
                        transmitterDate.SendData($"Time is:{DateTime.Now.Hour}:{DateTime.Now.Minute}.{DateTime.Now.Second}.{DateTime.Now.Millisecond}").Wait();
                    }
                }
            });

            while (true) ;
        }
    }
}
