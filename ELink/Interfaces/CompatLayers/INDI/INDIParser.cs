using ELink.Interfaces.CompatLayers.INDI.ParsingDevices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace ELink.Interfaces.CompatLayers.INDI
{
    public class INDIParser
    {
        private TcpClient tcpClient;
        private Stream stream;
        private string serverAdress;
        private int port;


        private Dictionary<string, List<XElement>> deviceData = new Dictionary<string, List<XElement>>();
        private Dictionary<string, IINDIDevice> devices = new Dictionary<string, IINDIDevice>();


        private object streamLockObject = new object();

        public INDIParser(string serverAdress, int port)
        {
            this.serverAdress = serverAdress;
            this.port = port;
        }
        public void SendCommand(string command)
        {
            var bytes = Encoding.UTF8.GetBytes(command);
            lock(streamLockObject)
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        public void Start()
        {
            Task.Run(() =>
            {
                try
                {
                    tcpClient = new TcpClient();
                    tcpClient.Connect(serverAdress, port);
                    Run().Wait();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }).ContinueWith(x => Console.WriteLine("TaskDone"));
        }
        private async Task Run()
        {
            stream = tcpClient.GetStream();

            SendCommand("<getProperties version=\"1.7\"/>\n");

            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                Async = true
            });

            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    var el = await XNode.ReadFromAsync(reader, CancellationToken.None) as XElement;
                    var device = el.Attribute("device").Value;

                    if (!devices.ContainsKey(device))
                    {
                        if (!deviceData.ContainsKey(device))
                        {
                            deviceData[device] = new List<XElement>();
                        }
                        deviceData[device].Add(el);

                        IINDIDevice? knowItem = GetDefinitiveID(el);

                        if (knowItem != null)
                        {
                            Console.WriteLine(knowItem.Name);
                            foreach (var property in deviceData[knowItem.Name])
                            {
                                knowItem.ParseProperty(property);
                            }
                            devices[knowItem.Name] = knowItem;
                            knowItem.Setup();
                        }
                    }
                    else
                    {
                        devices[device].ParseProperty(el);
                    }
                }
            }
        }
        public string GetINDITimeStamp()
        {
            var time = DateTime.Now;
            return $"{time.Year}-{time.Month}-{time.Day}T{time.Hour}:{time.Minute}:{time.Second}";
        }
        private IINDIDevice? GetDefinitiveID(XElement el)
        {
            switch (el.Attribute("name")?.Value)
            {
                case "EQUATORIAL_COORD":
                    //Console.WriteLine($"\tTelescope: {el.Attribute("device")?.Value}");
                    break;
                case "CCD_EXPOSURE":
                    return new INDICamera(el.Attribute("device")?.Value,this);
                case "FILTER_SLOT":
                    //Console.WriteLine($"\tFilter Wheel: {el.Attribute("device")?.Value}");
                    break;
                case "FOCUS_SPEED":
                    //Console.WriteLine($"\tEAF: {el.Attribute("device")?.Value}");
                    break;
                default: 
                    return null;
            }
            return null;
        }
    }
}
