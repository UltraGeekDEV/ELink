using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections.Models
{
    public class ExsessivePackageSizeException : Exception
    {
        public ExsessivePackageSizeException(string msg): base(msg)
        {
        }
    }
}
