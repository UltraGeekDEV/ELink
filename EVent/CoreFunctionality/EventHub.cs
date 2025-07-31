using EVent.Comms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class EventHub
    {
        private Dictionary<string,List<IComms>> connectedEvents = new Dictionary<string, List<IComms>>();
        private ConnectionHub connectionHub = new ConnectionHub();
        public void Setup()
        {

        }
    }
}
