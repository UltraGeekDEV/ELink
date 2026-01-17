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
            EventHub hubA = new EventHub(new List<IServer> { new TCPServer(IPAddress.Any,8594,"HubA")}, "HubA");
            EventHub hubC = new EventHub(new List<IServer> { new TCPServer(IPAddress.Any, 8599, "HubC") }, "HubC");
            hubA.Setup();
            hubC.Setup();

            //Thread.Sleep(1000);

            var client = TCPClientConnection.ConnectAsTransmitter("HubA");
            var clientC = TCPClientConnection.ConnectAsTransmitter("HubC");

            client.OnDataRecieved(x =>
            {
                var data = new BinaryConvertableString();
                data.FromBytes(x.Data);
                Console.WriteLine($"ClientA via HubA: {data}");
            });
            clientC.OnDataRecieved(x =>
            {
                var data = new BinaryConvertableString();
                data.FromBytes(x.Data);
                Console.WriteLine($"ClientC via HubA then HubC: {data}");
            });

            var connection = new Package() { EventID = "InitiateInterconnect"
                , type = PackageType.ServerAdminEvent
                , Data = new TCPConnectionData() { IP = "127.0.0.1", Port = 4500 }.ToBytes()};

            var connectionB = new Package(){
                EventID = "InitiateInterconnect"
                ,
                type = PackageType.ServerAdminEvent
                ,
                Data = new TCPConnectionData() { IP = "127.0.0.1", Port = 8594 }.ToBytes()};

            Thread.Sleep(100);

            client.SendData(connection);
            clientC.SendData(connectionB);

            //Thread.Sleep(5000);

            client.HookEvent("Test");
            clientC.HookEvent("Test");

            while (true) ;
        }
    }
}
