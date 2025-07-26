using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Models.Data.Coordinates
{
    public struct EquatorialCoordiantes
    {
        public double Ra { get; set; }
        public double Dec { get; set; }

        public EquatorialCoordiantes()
        {

        }

        public static implicit operator EquatorialAndRotationCoordinates(EquatorialCoordiantes EQ)
        {
            return new EquatorialAndRotationCoordinates { Ra = EQ.Ra, Dec = EQ.Dec, R = 0 };
        }
    }
}
