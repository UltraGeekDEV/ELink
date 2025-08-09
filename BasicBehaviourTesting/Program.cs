using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.TCP;
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
            

            Task.Delay(1000).Wait();

            Console.WriteLine("Recievers: A / B");
            var clientA = TCPClientConnection<BinaryConvertableString>.ConnectAsReciever("A", "127.0.0.1", 5000);
            clientA.OnDataRecieved(message =>
            {
                Console.WriteLine($"A recieved: {message}");
            });
            var clientB = TCPClientConnection<BinaryConvertableString>.ConnectAsReciever("B", "127.0.0.1", 5000);
            clientB.OnDataRecieved(message =>
            {
                Console.WriteLine($"B recieved: {message}");
            });
            while (true) ;
        }
    }
}
