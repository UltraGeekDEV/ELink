using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ELink.Interfaces.CompatLayers.INDI
{
    public interface IINDIDevice
    {
        string Name { get; }
        public void ParseProperty(XElement property);
        public void Setup();
    }
}
