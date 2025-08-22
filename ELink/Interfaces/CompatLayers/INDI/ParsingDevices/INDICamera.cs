using ELink.Interfaces.Utils;
using ELink.Models.Data.Capture;
using ELink.Models.Utils.Comms;
using EVent.Connections.TCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Reflection.Metadata.BlobBuilder;

namespace ELink.Interfaces.CompatLayers.INDI.ParsingDevices
{
    public class INDICamera : IINDIDevice
    {
        public string Name { get; private set; }
        INDIParser parser;

        public INDICamera(string DeviceName, INDIParser parser)
        {
            Name = DeviceName;
            this.parser = parser;
        }

        public void Setup()
        {
            var captureFrameEvent = TCPClientConnection<CaptureFrame>.ConnectAsReciever(Events.CaptureFrame, ConnectionInfo.EVentServer, ConnectionInfo.EVentPort);
            captureFrameEvent.OnDataRecieved(INDICaptureFrame);
        }

        private void INDICaptureFrame(CaptureFrame frame)
        {
            Console.WriteLine($"Capture frame of {frame.exposureLength}s with gain:{frame.gain}");
            Task.Run(() =>
            {
                parser.SendCommand($"<enableBLOB device=\"{Name}\">Also</enableBLOB>");
                parser.SendCommand($"<newNumberVector  device=\"{Name}\" name=\"CCD_EXPOSURE\" state=\"Busy\" timeout=\"60\" timestamp=\"{parser.GetINDITimeStamp()}\">\n<oneNumber name=\"CCD_EXPOSURE_VALUE\">\n{frame.exposureLength}\n</oneNumber>\n</newNumberVector >\n");
            });
        }

        public void ParseProperty(XElement property)
        {
            if (property.Attribute("name")?.Value == "CCD_EXPOSURE")
            {
                foreach (var item in property.Elements())
                {
                    Console.WriteLine($"Exposure left:{double.Parse(item.Value).ToString("0.##")}");
                }
            }
            else if (property.Attribute("name")?.Value == "CCD1")
            {
                foreach (var item in property.Elements())
                {
                    try
                    {
                        Console.WriteLine("BLOB recieved for image");
                        CreateIamge(item.Value);
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine( ex.ToString()); 
                    }
                }
            }
        }
        private void CreateIamge(string data)
        {
            var blob = Convert.FromBase64String(data);

            int headerLength = 0;
            Console.WriteLine("Fits header:");
            for (int i = 0; i < blob.Length; i += 2880)
            {
                string block = Encoding.ASCII.GetString(blob, i, 2880);
                headerLength += 2880;

                var records = Enumerable.Range(0, block.Length / 80)
                        .Select(i => block.Substring(i * 80, 80))
                        .ToArray();

                foreach (var item in records)
                {
                    if (item.Contains('='))
                    {
                        var lineItem = new FITSHeaderItem(item);
                        Console.WriteLine($"\t{item}");
                    }
                }
                if (block.Contains(" END ")) break;
            }
        }
    }
}
