using EVent.Connections.TCP;
using System.Net.Sockets;

namespace BasicBehaviourTesting
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TCPServer tcpServer = new TCPServer();
            var serverTask = Task.Run(tcpServer.Run);

            while(true)
            {
                try
                {
                    TcpClient tcpClient = new TcpClient();
                    tcpClient.Connect("127.0.0.1", 5000);
                    var stream = tcpClient.GetStream();
                    var time = DateTime.Now;
                    var data = BitConverter.GetBytes(time.Second);
                    stream.Write(data, 0, data.Length);
                    var dataBack = new byte[data.Length];
                    stream.Read(dataBack, 0, data.Length);
                    var secondsBack = BitConverter.ToInt32(dataBack);
                    Console.WriteLine($"Sent: {time.Second}\t Recieved: {secondsBack}");
                    Thread.Sleep(100);

                    stream.Close();
                    stream.Dispose();
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
