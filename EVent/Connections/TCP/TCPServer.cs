using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.UDP;
using EVent.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : IServer
    {
        Action<PackageInfo>? DataRecieved;
        Action<string,IServer>? AddedEvent;
        Action<string,IServer>? RemovedEvent;
        TcpListener tcpListener;
        List<ServerTCPBroadcast> discoveryBroadcastChannels;
        Task mainThread;
        bool IsAlive = true;

        IPAddress listeningAdress;
        int port;

        Dictionary<string, HashSet<TcpClient>> events = new Dictionary<string, HashSet<TcpClient>>();
        object eventLock = new object();
        public TCPServer(IPAddress listeningAdress, int port)
        {
            this.listeningAdress = listeningAdress;
            this.port = port;
        }
        public TCPServer(IPAddress listeningAdress, int port,params string[] serverBroadcastedIDs) : this(listeningAdress, port)
        {
            IPAddress localIP = IPUtils.GetLocalIPv4();

            discoveryBroadcastChannels = serverBroadcastedIDs.Select(x =>
            {
                var broadcast = new ServerTCPBroadcast(x, localIP.ToString(), port.ToString());
                broadcast.Start();
                return broadcast;
            }).ToList();
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

            if (handhsake.type == PackageType.ConnectEvent)
            {
                HookEvents(client, handhsake);
                Task.Run(() => RunClient(client, handhsake.EventID));
            }
            else
            {   
                Debug.WriteLine($"\tTransmitter recieved");
                DataRecieved?.Invoke(handhsake);
                Task.Run(() => RunClient(client, null));
            }
        }

        private void HookEvents(TcpClient client, PackageInfo handhsake)
        {
            Debug.WriteLine($"\tReciever recieved on event: {handhsake.EventID}");
            var listeningOnEvents = handhsake.EventID.Split('|').ToList();
            lock (eventLock)
            {
                foreach (var eventID in listeningOnEvents)
                {
                    if (!events.ContainsKey(eventID))
                    {
                        events[eventID] = new HashSet<TcpClient>();
                        AddedEvent?.Invoke(eventID, this);
                    }

                    events[eventID].Add(client);
                }
            }
        }
        private void UnhookEvents(TcpClient client, IEnumerable<string> eventIDs)
        {
            lock (eventLock)
            {
                foreach (var eventID in eventIDs)
                {
                    if (events.ContainsKey(eventID) && events[eventID].Contains(client))
                    {
                        events[eventID].Remove(client);

                        if (events[eventID].Count == 0)
                        {
                            events.Remove(eventID);
                            RemovedEvent?.Invoke(eventID, this);
                        }
                    }
                }
            }
        }

        private async void RunClient(TcpClient client,string eventID)
        {
            HashSet<string> events;
            if (eventID != null)
            {
                events = eventID.Split('|').Distinct().ToHashSet();
            }
            else
            {
                events = new HashSet<string>();
            }
            try
            {
                var stream = client.GetStream();

                PackageInfo? packageInfo = null;
                while ((packageInfo = await PackageInfo.ReadPackage(stream)) != null)
                {
                    Debug.WriteLine("Message Recived");
                    switch (packageInfo.type)
                    {
                        case PackageType.ConnectEvent:
                            {   
                                foreach (var newEvent in packageInfo.EventID.Split('|').Distinct())
                                {
                                    if (!events.Contains(newEvent))
                                    {
                                        events.Add(newEvent);
                                    }
                                }

                                HookEvents(client, packageInfo);
                                break;
                            }   


                        case PackageType.DisconnectEvent:
                            {
                                var eventsToUnhook = packageInfo.EventID.Split('|').Distinct().ToList();
                                UnhookEvents(client, eventsToUnhook);
                                break;
                            }


                        default:
                            {
                                DataRecieved?.Invoke(packageInfo);
                                break;
                            }
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
                    DropClient(client, events);
                }
            }
            
        }
        private void DropClient(TcpClient client,IEnumerable<string> eventIDs)
        {
            UnhookEvents(client,eventIDs);

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

                return ret;
            }).Distinct().ToList();
            
            if (clientSnaphsot.Count == 0)
            {
                return;
            }

            var tasks = clientSnaphsot.Select(async listeningClient => {
                try
                {
                    var clientPackage = new PackageInfo() { Data = package.Data, type = package.type,EventID = package.EventID };
                    var sendStream = listeningClient.GetStream();
                    var sendData = clientPackage.ToBytes();
                    await sendStream.WriteAsync(sendData);
                }
                catch 
                {
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
