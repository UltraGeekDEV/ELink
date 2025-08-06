using EVent.Comms;
using EVent.Connections.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : IConnection
    {
        Action<PackageInfo>? DataRecieved;
        TcpListener tcpListener;
        Task mainThread;
        bool IsAlive = true;

        Dictionary<string, List<TcpClient>> events = new Dictionary<string, List<TcpClient>>();
        object eventLock = new object();

        public void Run(IPAddress listeningAdress,int port)
        {
            Debug.WriteLine("Server started");
            //for testing purposes, directly relay event back to clients
            DataRecieved += async x => await SendData(x);
            mainThread = Task.Run(() =>
            {
                tcpListener = new TcpListener(listeningAdress, port);
                tcpListener.Start();
                while (IsAlive)
                {
                    var client = tcpListener.AcceptTcpClient();
                    AcceptClient(client);
                }
            });   
        }
        private async Task AcceptClient(TcpClient client)
        {
            var stream = client.GetStream();

            PackageInfo? handhsake;
            try
            {
                handhsake = await PackageInfo.ReadPackage(stream);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Connection threw");
                return;
            }

            if (handhsake is null || handhsake.type == PackageType.Invalid) 
            {
                Debug.WriteLine("Package was malformed");
            }

            if (handhsake.type == PackageType.Handshake)
            {
                Debug.WriteLine($"\tReciever recieved on event: {handhsake.EventID}");
                lock (eventLock)
                {
                    if (!events.ContainsKey(handhsake.EventID))
                    {
                        events[handhsake.EventID] = new List<TcpClient>();
                    }

                    events[handhsake.EventID].Add(client);
                }
                Task.Run(() => RunClient(client, handhsake.EventID));
            }
            else
            {   
                Debug.WriteLine($"\tTransmitter recieved");
                DataRecieved?.Invoke(handhsake);
                Task.Run(() => RunClient(client, null));
            }
        }
        private async void RunClient(TcpClient client,string eventID)
        {
            try
            {
                var stream = client.GetStream();

                PackageInfo? packageInfo = null;
                while ((packageInfo = await PackageInfo.ReadPackage(stream)) != null)
                {
                    if (packageInfo.type != PackageType.Invalid)
                    {
                        DataRecieved?.Invoke(packageInfo);
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception on server: {ex.Message}");
            }
            finally
            {
                if (eventID != null)
                {
                    DropCLient(client, eventID);
                }
            }
            
        }
        private void DropCLient(TcpClient client,string eventID)
        {
            lock (eventLock)
            {
                if (events.ContainsKey(eventID) && events[eventID].Contains(client))
                {
                    events[eventID].Remove(client);

                    if (events[eventID].Count == 0)
                    {
                        events.Remove(eventID);
                    }
                }
            }

            client.Close();
        }

        public async Task SendData(PackageInfo package)
        {
            var clientSnaphsot = package.EventID.Split('|').SelectMany(EventID =>
            {
                List<TcpClient> ret;
                lock (eventLock)
                {
                    if (events.ContainsKey(EventID))
                    {
                       ret = events[EventID].ToList();
                    }
                    else
                    {
                        ret = new List<TcpClient>();
                    }
                }

                return ret.Select(client => new
                {
                    client = client,
                    EventID = EventID
                });
            }).ToList();
            
            if (clientSnaphsot.Count == 0)
            {
                return;
            }

            var tasks = clientSnaphsot.Select(async listeningClient => {
                try
                {
                    var clientPackage = new PackageInfo() { Data = package.Data, type = package.type,EventID = listeningClient.EventID };
                    var sendStream = listeningClient.client.GetStream();
                    var sendData = clientPackage.ToBytes();
                    byte[] dataLen = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(dataLen, sendData.Length);
                    await sendStream.WriteAsync(dataLen);
                    await sendStream.WriteAsync(sendData);
                }
                catch 
                {
                    DropCLient(listeningClient.client, listeningClient.EventID);
                }
            });
            await Task.WhenAll(tasks);
        }

        public void OnDataRecieved(Action<PackageInfo> handler)
        {
            DataRecieved += handler;
        }

        public void Stop()
        {
            IsAlive = false;
        }
    }
}
