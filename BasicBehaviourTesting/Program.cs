using EVent.Connections;
using EVent.Connections.Models;
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
            var clientA = TCPClientConnection.ConnectAsReciever("A", "127.0.0.1", 5000);
            clientA.OnDataRecieved(package =>
            {
                var message = Encoding.UTF8.GetString(package.Data);
                Console.WriteLine($"A recieved: {message}");
            });
            var clientB = TCPClientConnection.ConnectAsReciever("B", "127.0.0.1", 5000);
            clientB.OnDataRecieved(package =>
            {
                var message = Encoding.UTF8.GetString(package.Data);
                Console.WriteLine($"B recieved: {message}");
            });
            while (true) ;
        }
    }
}
