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
            var clientA = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("A", "EVentTestServer");
            var clientC = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("C|D", "EVentTestServer");
            var clientD = TCPClientConnection<BinaryConvertableString>.ConnectAsTransmitter("D", "EVentTestServer");

            var clientTask = Task.Run(async () =>
            {
                while (clientA.IsAlive)
                {
                    Console.WriteLine("PleaseEnterMessage");
                    Console.ReadLine();
                    clientA.SendData(data: "\nThis is client A");
                    Console.ReadLine();
                    clientC.SendData(data: "This is client C|D");
                    //clientD.SendData(data: "This is client D");
                }
            });

            while (true) ;
        }
    }
}
