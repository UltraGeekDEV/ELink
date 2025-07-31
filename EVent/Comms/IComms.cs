using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Comms
{
    public interface IComms<T>
    {
        public void Send(T data);
    }
}
