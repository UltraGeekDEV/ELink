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

namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var transmitterText = TCPClientConnection<CaptureFrame>.ConnectAsTransmitter(Events.CaptureFrame, "127.0.0.1", 5000);

            var clientTask = Task.Run(async () =>
            {
                while (transmitterText.IsAlive)
                {
                    Console.WriteLine("Give exposure and gain");
                    var frame = new CaptureFrame() { exposureLength = double.Parse(Console.ReadLine()), gain = double.Parse(Console.ReadLine()) };
                    await transmitterText.SendData(frame);
                    Task.Delay(1000).Wait();
                }
            });

            while (true) ;
        }
    }
}
