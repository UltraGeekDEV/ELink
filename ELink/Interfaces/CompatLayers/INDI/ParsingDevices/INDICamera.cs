using ELink.Interfaces.Utils;
using ELink.Models.Data.Capture;
using ELink.Models.Data.Image;
using ELink.Models.Utils.Comms;
using EVent.Connections.TCP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
                        var img = CreateImage(item.Value);
                        //TODO:: HANDLE IMAGE
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine( ex.ToString()); 
                    }
                }
            }
        }
        private ELinkImage? CreateImage(string data)
        {
            try
            {
                var blob = Convert.FromBase64String(data);
                var image = new ELinkImage();

                //File.WriteAllBytes("./debug.fits", blob);

                int headerLength = 0;
                Console.WriteLine("Fits header:");
                int i = 0;
                for (; i < blob.Length; i += 2880)
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
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.Write($"\t{lineItem.key}");
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.Write(" = ");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write($"\t{lineItem.value}");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write($"\t/ {lineItem.comment}\n");
                            image.FitsHeader[lineItem.key] = lineItem;
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Gray;
                    if (block.Contains(" END ")) break;
                }

                if (image.FitsHeader.Count == 0)
                {
                    Console.WriteLine("Skipping empty image");
                    return null;
                }

                var outputDataSize = (blob.Length - i) / 2;
                var bzero = int.Parse(image.FitsHeader["BZERO"].value);
                var bscale = float.Parse(image.FitsHeader["BSCALE"].value);

                var nAxis = int.Parse(image.FitsHeader["NAXIS"].value);
                var width = int.Parse(image.FitsHeader["NAXIS1"].value);
                var height = int.Parse(image.FitsHeader["NAXIS2"].value);

                image.Width = width/2;
                image.Height = height/2;

                var isCol = nAxis == 3;
                var isBayer = image.FitsHeader.ContainsKey("BAYERPAT");
                var bayerName = image.FitsHeader["BAYERPAT"].value;
                var bayerXOffset = int.Parse(image.FitsHeader["XBAYROFF"].value);
                var bayerYOffset = int.Parse(image.FitsHeader["YBAYROFF"].value);

                int[,] bayerMatrix = new int[,] {
                {bayerName[1] == 'R' ? 0 : bayerName[1] == 'G' ? 1 : 2, bayerName[2] == 'R' ? 0 : bayerName[2] == 'G' ? 1 : 2 },
                {bayerName[3] == 'R' ? 0 : bayerName[3] == 'G' ? 1 : 2 ,bayerName[4] == 'R' ? 0 : bayerName[4] == 'G' ? 1 : 2} };

                var getXY = (int pos) => { return (x: (pos % width), y: (pos / width)); };

                if (isBayer)
                {
                    image.Data = new float[image.Width*image.Height*3];
                }
                else
                {
                    image.Data = new float[outputDataSize];
                }

                int upTo = width * height * 2;
                float avg = 0.0f;
                int count = 0;
                i += 2880;
                for (int j = 0; j < upTo; j += 2)
                {
                    short raw = (short)((blob[j + i] << 8) | blob[j + i+1]);
                    float value = (raw * bscale + bzero) / (float)ushort.MaxValue*50;
                    
                    avg = (value + (avg*count))/(count+1);
                    count++;

                    if (isBayer)
                    {
                        var pos = getXY(j / 2);

                        var bayerOffset = bayerMatrix[(pos.x + bayerXOffset) % 2, (pos.y + bayerYOffset) % 2];

                        var outputIndex = ((pos.x/2) * 3 + bayerOffset + (pos.y/2) * image.Width*3);

                        if (image.Data[outputIndex] < float.Epsilon)
                        {
                            image.Data[outputIndex] = value;
                        }
                        else
                        {
                            image.Data[outputIndex] = 0.5f * (value + image.Data[outputIndex]);
                        }
                    }
                    else
                    {
                        image.Data[j/2] = value;
                    }
                }
                Console.WriteLine($"Average pixel value is:{avg}");
                if (isBayer || isCol)
                {
                    image.Type = ImageType.Color;
                }
                else
                {
                    image.Type = ImageType.Mono;
                }

                if (image.FitsHeader.ContainsKey("FILTER"))
                {
                    image.Filter = image.FitsHeader["FILTER"].value.Remove('\'').Trim();
                }
                else
                {
                    image.Filter = "OSC";
                }

                return image;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                return null;
            }
        }
    }
}
