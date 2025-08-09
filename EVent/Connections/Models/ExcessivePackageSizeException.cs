using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models
{
    public class ExcessivePackageSizeException : Exception
    {
        public ExcessivePackageSizeException(string msg): base(msg)
        {
        }
    }
}
