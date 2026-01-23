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
        private static string MeasureTime(DateTime startTime)
        {
            var delta = DateTime.Now - startTime;
            return $"{(int)delta.TotalMilliseconds}.{delta.Microseconds}";
        }
        static void Main(string[] args)
        {
            EventHub hubA = new EventHub("HubA",new TCPServer(IPAddress.Any,8594,"HubA"));
            EventHub hubC = new EventHub("HubC",new TCPServer(IPAddress.Any, 8599, "HubC") );

            hubA.Setup();
            hubC.Setup();
            var client = TCPClientConnection.Connect("HubA");
            var clientC = TCPClientConnection.Connect("HubC");
            var clientE = TCPClientConnection.Connect("HubA");
            var startTime = DateTime.Now;

            clientE.OnDataRecieved(x =>
            {
                var data = new BinaryConvertableString();
                if (x.Data.Length == 0)
                {
                    Console.WriteLine($"{MeasureTime(startTime)} HubA: {x.EventID}");
                }
                else if (data.FromBytes(x.Data))
                {
                    Console.WriteLine($"{MeasureTime(startTime)} HubA: {x.EventID} : {data}");
                }
                else
                {
                    var connectionData = new TCPConnectionData();
                    Console.WriteLine($"{MeasureTime(startTime)} HubA: {x.EventID} : {connectionData.IP}:{connectionData.Port}");
                }
            });
            clientE.HookEvent("EventRemoved");
            clientE.HookEvent("EventAdded");
            clientE.HookEvent("CreateInterconnect");
            clientE.HookEvent("UpgradeToInterconnect");
            clientC.HookEvent("Test");

            Task.Delay(1000).Wait();

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
            Task.Delay(10000).Wait();
            clientC.SendData(connectionB);

            while (true)
            {
                //Console.WriteLine("Write your message");
                BinaryConvertableString message = Console.ReadLine();
                var package = new Package() { EventID = "Test", type = PackageType.Data, Data = message.ToBytes() };
                client.SendData(package);
            }

            while (true) ;
        }
    }
}
