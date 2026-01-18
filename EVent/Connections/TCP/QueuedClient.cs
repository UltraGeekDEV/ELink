using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class QueuedClient
    {
        private IStreamClient? client;
        private Channel<Package> sendCommandQueue;
        private bool loopRunning;

        public QueuedClient()
        {
            sendCommandQueue = Channel.CreateUnbounded<Package>();
        }
        public QueuedClient(IStreamClient client)
        {
            sendCommandQueue = Channel.CreateUnbounded<Package>();
            this.client = client;
            loopRunning = true;
            _ = EmptyQueue();
        }

        public void SetClient(IStreamClient client)
        {
            this.client = client;
            loopRunning = true;
            _ = EmptyQueue();
        }

        public async Task Send(Package package)
        {
            await sendCommandQueue.Writer.WriteAsync(package);
        }

        private async Task EmptyQueue()
        {
            while (loopRunning)
            {
                var package = await sendCommandQueue.Reader.ReadAsync();
                try
                {
                    await client!.Send(package.ToBytes());
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception while sending data: {ex}");
                    break;
                }
            }
            client!.Close();
        }
        public async Task<Package?> ReadPackage()
        {
            return await client!.ReadPackage();
        }

        public void Shutdown()
        {
            sendCommandQueue.Writer.Complete();
            loopRunning = false;
        }
    }
}
