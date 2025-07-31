using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Comms
{
    public interface IComms
    {
        public void Send<T>(T data);
    }
}
