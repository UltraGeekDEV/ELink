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
using static System.Net.Mime.MediaTypeNames;

namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            EventHub hubB = new EventHub(new List<IServer> { new TCPServer(IPAddress.Any, 4500,"HubB") }, "HubB");
            hubB.Setup();

            var client = TCPClientConnection.ConnectAsTransmitter("HubB");
            var clientD = TCPClientConnection.ConnectAsTransmitter("HubB");

            clientD.OnDataRecieved(x =>
            {
                var text = new BinaryConvertableString();
                text.FromBytes(x.Data);
                Console.WriteLine($"ClientD received: {text}");
            });

            clientD.HookEvent("Test");

            while (true)
            {
                Console.WriteLine("Write your message");
                BinaryConvertableString message = Console.ReadLine();
                var package = new PackageInfo() { EventID = "Test", type = PackageType.Data, Data = message.ToBytes(), Sender = "ClientB" };
                client.SendData(package);
            }

            while (true) ;
        }
    }
}
