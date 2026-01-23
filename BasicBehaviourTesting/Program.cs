using ELink.Interfaces.CompatLayers.INDI;
using ELink.Models.Utils.Comms;
using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.TCP;
using EVent.CoreFunctionality;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Unicode;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BasicBehaviourTesting
{
    internal class Program
    {
        static void Main(string[] args)
        {
            EventHub hubA = new EventHub("HubA",new TCPServer(IPAddress.Any,8594,"HubA"));
            EventHub hubC = new EventHub("HubC",new TCPServer(IPAddress.Any, 8599, "HubC") );
            Task.Delay(1000).Wait();
            hubA.Setup();
            hubC.Setup();
            var client = TCPClientConnection.Connect("HubA");
            var clientC = TCPClientConnection.Connect("HubC");
            var clientE = TCPClientConnection.Connect("HubA");

            clientE.OnDataRecieved(x =>
            {
                var data = new BinaryConvertableString();
                data.FromBytes(x.Data);
                Console.WriteLine($"ClientE via HubA: {data}");
            });
            clientC.OnDataRecieved(x =>
            {
                var data = new BinaryConvertableString();
                data.FromBytes(x.Data);
                Console.WriteLine($"ClientC via HubA then HubC: {data}");
            });

            var connection = new Package() { EventID = "CreateInterconnect"
                , type = PackageType.ServerAdminEvent
                , Data = new TCPConnectionData() { IP = "127.0.0.1", Port = 4500 }.ToBytes()};

            var connectionB = new Package(){
                EventID = "CreateInterconnect"
                ,
                type = PackageType.ServerAdminEvent
                ,
                Data = new TCPConnectionData() { IP = "127.0.0.1", Port = 8594 }.ToBytes()};

            client.SendData(connection);
            clientC.SendData(connectionB);

            //clientE.HookEvent("Test");
            clientC.HookEvent("Test");

            while (true)
            {
                Console.WriteLine("Write your message");
                BinaryConvertableString message = Console.ReadLine();
                var package = new Package() { EventID = "Test", type = PackageType.Data, Data = message.ToBytes() };
                client.SendData(package);
            }

            while (true) ;
        }
    }
}
