using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class EventHub
    {
        private Dictionary<IServer, List<string>> servers = new Dictionary<IServer, List<string>>();
        private object eventsLock = new object();
        private Dictionary<IServer,object> connectionLocks;
        public EventHub(List<IServer> connections)
        {
            servers = connections.ToDictionary(x=>x,y=>new List<string>());
            connectionLocks = connections.Select(x => new { connection = x, lockObject = new object() }).ToDictionary(x => x.connection, x => x.lockObject);
        }
        public void Setup()
        {
            foreach (var connection in servers.Keys)
            {
                connection.OnEventAdded(AddEvent);
                connection.OnEventRemoved(RemoveEvent);
                connection.OnDataRecieved(DataRecieved);
                connection.Run();
            }
        }
        private void AddEvent(string eventID,IServer server)
        {
            Debug.WriteLine($"Added Event: {eventID}");
            lock (eventsLock)
            {
                servers[server].Add(eventID);
            }
        }
        private void RemoveEvent(string eventID, IServer server)
        {
            Debug.WriteLine($"Removed Event: {eventID}");
            lock (eventsLock)
            {
                servers[server].Remove(eventID);
            }
        }
        private void DataRecieved(PackageInfo package)
        {
            var eventList = package.EventID.Split('|').ToHashSet();
            Dictionary<IServer, List<string>> serversCopy;
            lock (eventsLock)
            {
                serversCopy = servers.ToDictionary();
            }
            var serverList = serversCopy.Where(x => x.Value.Any(y => eventList.Contains(y))).Select(x=>x.Key).ToList();
            if (servers.Count == 0)
            {
                return;
            }

            foreach (var server in serverList)
            {
                lock (connectionLocks[server])
                {
                    server.SendData(package);
                }
            }
        }
    }
}
