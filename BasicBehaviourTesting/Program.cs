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
            ConnectionInfo.EVentServer = "127.0.0.1";
            ConnectionInfo.EVentPort = 5000;

            EventHub hub = new EventHub(new List<IServer>() { new TCPServer(IPAddress.Any, ConnectionInfo.EVentPort) });
            hub.Setup();

            INDIParser parser = new INDIParser("192.168.0.68", 7624);
            parser.Start();


            while (true) ;
        }
    }
}
