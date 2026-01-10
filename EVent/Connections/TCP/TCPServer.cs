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
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : IServer
    {
        Action<PackageInfo, IServer>? DataRecieved;
        Action<PackageInfo, IServer>? InterconnectDataRecievedEvent;
        Action<string,IServer>? AddedEvent;
        Action<string,IServer>? RemovedEvent;
        TcpListener tcpListener;
        List<ServerTCPBroadcast> discoveryBroadcastChannels;
        Task mainThread;
        bool IsAlive = true;

        IPAddress listeningAdress;
        int port;

        Dictionary<string, HashSet<TcpClient>> events = new Dictionary<string, HashSet<TcpClient>>();
        Dictionary<TcpClient, HashSet<string>> interconnectEvents = new Dictionary<TcpClient, HashSet<string>>();
        object interconnectLock = new object();
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
            
            mainThread = Task.Run(async () =>
            {
                try
                {
                    tcpListener = new TcpListener(listeningAdress, port);
                    tcpListener.Start();
                    while (IsAlive)
                    {
                        var client = tcpListener.AcceptTcpClient();
                        await AcceptClient(client);
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

            switch (handhsake.type)
            {
                case PackageType.ConnectEvent:
                    {
                        HookEvents(client, handhsake);
                        Task.Run(() => RunClient(client, handhsake.EventID));
                        break;
                    }
                case PackageType.ConnectInterconnect:
                    {
                        var connectionData = new TCPConnectionData();
                        
                        if (connectionData.FromBytes(handhsake.Data))
                        {
                            Task.Run(() => RunInterconnect(connectionData.IP,connectionData.Port));
                            break;
                        }
                        else 
                        {
                            return;
                        }
                    }
                case PackageType.ConnectFromInterconnect:
                    {
                        Task.Run(() => RunInterconnect(client));
                        break;
                    }
                default:
                    {
                        Debug.WriteLine($"\tTransmitter recieved");
                        DataRecieved?.Invoke(handhsake, this);
                        Task.Run(() => RunClient(client, null));
                        break;
                    }
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

                        case PackageType.ConnectInterconnect:
                            {
                                var connectionData = new TCPConnectionData();

                                if (connectionData.FromBytes(packageInfo.Data))
                                {
                                    Task.Run(() => RunInterconnect(connectionData.IP, connectionData.Port));
                                    break;
                                }
                                else
                                {
                                    return;
                                }
                            }

                        default:
                            {
                                SendData(packageInfo, client);
                                DataRecieved?.Invoke(packageInfo,this);
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
            var clientPackage = new PackageInfo() { Data = package.Data, type = package.type, EventID = package.EventID };
            var tasks = clientSnaphsot.Select(async listeningClient => {
                try
                {
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
        private async Task SendData(PackageInfo package,TcpClient receivedFrom)
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
            }).Distinct().Where(x=> x!= receivedFrom).ToList();

            if (clientSnaphsot.Count == 0)
            {
                return;
            }
            var clientPackage = new PackageInfo() { Data = package.Data, type = package.type, EventID = package.EventID };
            var tasks = clientSnaphsot.Select(async listeningClient => {
                try
                {
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
        public void OnDataRecieved(Action<PackageInfo,IServer> handler)
        {
            DataRecieved += handler;
        }
        public void OnInterconnectDataRecieved(Action<PackageInfo, IServer> handler)
        {
            InterconnectDataRecievedEvent += handler;
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
        private async void RunInterconnect(TcpClient tcpClient)
        {
            try
            {
                var stream = tcpClient.GetStream();
                PackageInfo handshake = new PackageInfo() { Data = new byte[0], EventID = "EstablishInterconnect", type = PackageType.ConnectFromInterconnect };
                stream.Write(handshake.ToBytes());
                while (tcpClient.Connected)
                {
                    var package = await PackageInfo.ReadPackage(stream);
                    if (package == null)
                    {
                        Debug.WriteLine("Interconnect recieved package was null");
                        continue;
                    }
                    if (package.type == PackageType.Invalid)
                    {
                        Debug.WriteLine("Interconnect recieved package was invalid");
                        continue;
                    }

                    InterconnectDataRecieved(package, tcpClient);
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine("Server connection forcibly closed");
                IsAlive = false;
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while running client: {ex}");
                await Task.Delay(1000);
            }
        }
        private async void RunInterconnect(string serverAdress, int serverPort)
        {
            TcpClient tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(serverAdress, serverPort);
                var stream = tcpClient.GetStream();
                PackageInfo handshake = new PackageInfo() { Data = new byte[0], EventID = "EstablishInterconnect", type = PackageType.ConnectFromInterconnect };
                stream.Write(handshake.ToBytes());
                while (tcpClient.Connected)
                {
                    var package = await PackageInfo.ReadPackage(stream);
                    if (package == null)
                    {
                        Debug.WriteLine("Interconnect recieved package was null");
                        continue;
                    }
                    if (package.type == PackageType.Invalid)
                    {
                        Debug.WriteLine("Interconnect recieved package was invalid");
                        continue;
                    }

                    InterconnectDataRecieved(package, tcpClient);
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine("Server connection forcibly closed");
                IsAlive = false;
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while running client: {ex}");
                await Task.Delay(1000);
            }
        }
        private async void InterconnectDataRecieved(PackageInfo package,TcpClient client)
        {
            switch (package.type)
            {
                case PackageType.ConnectFromInterconnect:
                case PackageType.ConnectEvent:
                    {
                        var eventIDs = package.EventID.Split('|').ToList();
                        lock (interconnectLock)
                        {
                            if (!interconnectEvents.ContainsKey(client))
                            {
                                interconnectEvents[client] = new HashSet<string>();
                            }

                            foreach (var eventID in eventIDs)
                            {
                                if (!interconnectEvents[client].Contains(eventID))
                                {
                                    interconnectEvents[client].Add(eventID);
                                }
                            }
                        }
                        package.type = PackageType.ConnectEvent;
                        await SendDataOnInterconnect(package, client);

                        break;
                    }
                case PackageType.DisconnectEvent:
                    {
                        var eventIDs = package.EventID.Split('|').ToList();
                        lock (interconnectLock)
                        {
                            foreach (var eventID in eventIDs)
                            {
                                if (interconnectEvents[client].Contains(eventID))
                                {
                                    interconnectEvents[client].Remove(eventID);
                                }
                            }
                        }

                        break;
                    }
                case PackageType.DisconnectInterconnect:
                    {
                        client.Close();
                        break;
                    }
                default:
                    {
                        await SendDataOnInterconnect(package, client);

                        InterconnectDataRecievedEvent?.Invoke(package, this);
                        await SendData(package);
                        break;
                    }
            }
        }
        private async Task SendDataOnInterconnect(PackageInfo package,TcpClient recievedFrom)
        {
            List<TcpClient> sendTo;
            lock (interconnectLock)
            {
                sendTo = package.EventID.Split('|').SelectMany(eventID => 
                interconnectEvents.Where(x => x.Key != recievedFrom &&(x.Value.Contains(eventID) 
                || package.type != PackageType.Data))
                       .Select(x => x.Key)).ToList();
            }

            var data = package.ToBytes();

            foreach (var partner in sendTo)
            {
                var stream = partner.GetStream();
                await stream.WriteAsync(data);
            }
        }
        public async Task SendDataOnInterconnect(PackageInfo package)
        {
            List<TcpClient> sendTo;
            lock (interconnectLock)
            {
                sendTo = package.EventID.Split('|').SelectMany(eventID => interconnectEvents.Where(x => x.Value.Contains(eventID) || package.type != PackageType.Data)
                        .Select(x => x.Key)).ToList();
            }

            var data = package.ToBytes();

            foreach(var partner in sendTo)
            {
                var stream = partner.GetStream();
                await stream.WriteAsync(data);
            }
        }
    }
}
