using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
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
    public class TCPServer : IServer
    {
        Action<PackageInfo>? DataRecieved;
        Action<string,IServer>? AddedEvent;
        Action<string,IServer>? RemovedEvent;
        TcpListener tcpListener;
        Task mainThread;
        bool IsAlive = true;

        IPAddress listeningAdress;
        int port;

        Dictionary<string, List<TcpClient>> events = new Dictionary<string, List<TcpClient>>();
        object eventLock = new object();
        public TCPServer(IPAddress listeningAdress, int port)
        {
            this.listeningAdress = listeningAdress;
            this.port = port;
        }
        public void Run()
        {
            Debug.WriteLine("Server started");
            
            mainThread = Task.Run(() =>
            {
                try
                {
                    tcpListener = new TcpListener(listeningAdress, port);
                    tcpListener.Start();
                    while (IsAlive)
                    {
                        var client = tcpListener.AcceptTcpClient();
                        AcceptClient(client);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception on the server{ex.Message}");
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
                return;
            }

            if (handhsake.type == PackageType.Handshake)
            {
                Debug.WriteLine($"\tReciever recieved on event: {handhsake.EventID}");
                lock (eventLock)
                {
                    if (!events.ContainsKey(handhsake.EventID))
                    {
                        events[handhsake.EventID] = new List<TcpClient>();
                        AddedEvent?.Invoke(handhsake.EventID,this);
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
                    Debug.WriteLine("Message Recived");
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
                        RemovedEvent?.Invoke(eventID,this);
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
        public void OnEventAdded(Action<string,IServer> handler)
        {
            AddedEvent += handler;
        }
        public void OnEventRemoved(Action<string, IServer> handler)
        {
            RemovedEvent += handler;
        }
        public void Stop()
        {
            IsAlive = false;
        }

        public bool HasEvent(string eventID)
        {
            lock (eventLock)
            {
                return events.ContainsKey(eventID);
            }
        }

        public bool HasEvent(IEnumerable<string> events)
        {
            IEnumerable<string> existingEvents;
            lock(eventLock)
            {
                existingEvents = this.events.Keys.ToList();
            }
            return existingEvents.AsParallel().Any(x => x.Equals(events));
        }
    }
}
