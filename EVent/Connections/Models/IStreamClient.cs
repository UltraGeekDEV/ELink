using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models
{
    public interface IStreamClient
    {
        public Task Send(byte[] data);
        public Task<Package?> ReadPackage();
        public void Close();
    }
}
