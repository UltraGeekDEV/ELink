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
        private static string MeasureTime(DateTime startTime)
        {
            var delta = DateTime.Now - startTime;
            return $"{(int)delta.TotalMilliseconds}.{delta.Microseconds}";
        }
        static void Main(string[] args)
        {
            EventHub hubB = new EventHub("HubB",new TCPServer(IPAddress.Any, 4500,"HubB"));
            hubB.Setup();

            var clientE = TCPClientConnection.Connect("HubB");
            var startTime = DateTime.Now;

            clientE.OnDataReceived(x =>
            {
                var data = new BinaryConvertableString();
                if (x.Data.Length == 0)
                {
                    Console.WriteLine($"{MeasureTime(startTime)} HubB: {x.EventID}");
                }
                else if (data.FromBytes(x.Data))
                {
                    Console.WriteLine($"{MeasureTime(startTime)} HubB: {x.EventID} : {data}");
                }
                else
                {
                    var connectionData = new TCPConnectionData();
                    Console.WriteLine($"{MeasureTime(startTime)} HubB: {x.EventID} : {connectionData.TargetIP}:{connectionData.TargetPort}");
                }
            });
            clientE.HookEvent("EventRemoved");
            clientE.HookEvent("EventAdded");
            clientE.HookEvent("CreateInterconnect");
            clientE.HookEvent("UpgradeToInterconnect");

            var client = TCPClientConnection.Connect("HubB");
            var clientD = TCPClientConnection.Connect("HubB");

            clientD.OnDataReceived(x =>
            {
                var text = new BinaryConvertableString();
                text.FromBytes(x.Data);
                Console.WriteLine($"ClientD received: {text}");
            });

            //Task.Run(() =>
            //{
            //    Task.Delay(10000).Wait();
            //    clientD.Stop();
            //});

            clientD.HookEvent("Test");

            while (true)
            {
                Console.WriteLine("Write your message");
                BinaryConvertableString message = Console.ReadLine();
                var package = new Package() { EventID = "Test", type = PackageType.Data, Data = message.ToBytes() };
                client.SendData(package);
            }
        }
    }
}
