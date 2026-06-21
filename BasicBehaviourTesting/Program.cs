using ELink.Interfaces.CompatLayers.INDI;
using ELink.Models.Data.Capture;
using ELink.Models.Data.Image;
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
using System.Drawing;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BasicBehaviourTesting
{
    internal class Program
    {

        static TCPClientConnection debugListener;
        static TCPClientConnection imageListener;
        static TCPClientConnection debugSender;
        static bool firstPass = true;
        static float prevFocus;
        static float curFocus;
        static float curInstantFocus;
        static bool imageReceivedBack = false;
        static float step = 360.0f;
        static float curPos = 0.0f;
        static void WaitForImage()
        {
            curFocus = 0.0f;
            imageReceivedBack = false;
            imageListener.SendData(new Package("TakeExposure", PackageType.Data, new CaptureFrame() { exposureLength = 1.0f }));
            while (!imageReceivedBack) { }
            curFocus += curInstantFocus;
        }
        static void Autofocus()
        {
            while (Math.Abs(step) > 1)
            {
                if (!firstPass)
                {
                    curPos += step;
                    debugSender.SendData(new Package("MoveAxis", PackageType.Data, (BinaryConvertableFloat)curPos));
                    Task.Delay(1000).Wait();
                    WaitForImage();
                    if (prevFocus > curFocus || Math.Abs(prevFocus-curFocus) < 0.5f)
                    {
                        step *= -0.5f;
                    }
                    prevFocus = curFocus;
                }
                else
                {
                    WaitForImage();
                    prevFocus = curFocus;
                    firstPass = false;
                }
            }
            firstPass = true;
        }
        static float GetSharpness(ELinkImage image)
        {
            float[,] laplacian = new float[image.Width, image.Height];
            if (image.Type == ImageType.Mono)
            {
                for (int loopJ = 1; loopJ < image.Height-1; loopJ++)
                {
                    for (int loopI = 1; loopI < image.Width-1; loopI++)
                    {
                        int i = loopI;
                        int j = loopJ;
                        int pos = i + j * image.Width;
                        laplacian[i, j] = image.Data[pos] * -8;
                        laplacian[i+1, j] += image.Data[pos + 1];
                        laplacian[i+1, j+1] += image.Data[pos+1+image.Width];
                        laplacian[i+1, j-1] += image.Data[pos + 1 - image.Width];
                        laplacian[i, j+1] += image.Data[pos + image.Width];
                        laplacian[i, j-1] += image.Data[pos - image.Width];
                        laplacian[i-1, j+1] += image.Data[pos - 1 + image.Width];
                        laplacian[i-1, j] += image.Data[pos-1];
                        laplacian[i-1, j-1] += image.Data[pos - 1 - image.Width];
                    }
                }
            }
            else
            {
                for (int loopJ = 1; loopJ < image.Height - 1; loopJ++)
                {
                    for (int loopI = 1; loopI < image.Width - 1; loopI++)
                    {
                        int i = loopI;
                        int j = loopJ;
                        int pos = i*3 + j * image.Width*3;
                        laplacian[i, j] = (image.Data[pos] + image.Data[pos+1] + image.Data[pos+2])/3.0f * -8;
                        i = loopI - 1; j = loopJ;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        j = loopJ - 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        j = loopJ + 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        i = loopI + 1; j = loopJ;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        j = loopJ - 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        j = loopJ + 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        i = loopI; j = loopJ - 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                        j = loopJ + 1;
                        pos = i + j * image.Width;
                        laplacian[i, j] += (image.Data[pos] + image.Data[pos + 1] + image.Data[pos + 2]) / 3.0f * 1;
                    }
                }
            }

            float average = 0.0f;
            int totalCount = laplacian.GetLength(0) * laplacian.GetLength(1);
            for (int i = 0; i < laplacian.GetLength(0); i++)
            {
                for (int j = 0; j < laplacian.GetLength(1); j++)
                {
                    average += laplacian[i, j] / totalCount;
                }
            }

            float meanSquare = 0;

            for (int i = 0; i < laplacian.GetLength(0); i++)
            {
                for (int j = 0; j < laplacian.GetLength(1); j++)
                {
                    meanSquare += MathF.Pow(laplacian[i, j] - average,2) / totalCount;
                }
            }
            curInstantFocus = meanSquare;
            return meanSquare;
        }
        static void Main(string[] args)
        {
            var eLinkHub = new EventHub("E-Link hub", new TCPServer(IPAddress.Any, 6721, "E-Link hub"));
            eLinkHub.Setup();
            var INDIParser = new INDIParser("192.168.0.52",7624);
            INDIParser.Start();

            debugListener = TCPClientConnection.Connect("E-Link hub");
            imageListener = TCPClientConnection.Connect("E-Link hub");
            debugSender = TCPClientConnection.Connect("E-Link hub");

            debugListener.HookEvent("DeviceAdded");
            debugListener.OnDataReceived(x =>
            {
                BinaryConvertableString data = new BinaryConvertableString();
                if (data.FromBytes(x.Data))
                {
                    imageListener.HookEvent($"ImageReceived-{data}");
                    Console.WriteLine(data);
                }

            });

            imageListener.OnDataReceived((x) =>
            {
                ELinkImage image = new ELinkImage();
                if (image.FromBytes(x.Data))
                {
                    GetSharpness(image);
                }
                imageReceivedBack = true;
            });

            while (true)
            {
                Console.ReadLine();
                Autofocus();
            }

            while (true);
        }
    }
}
