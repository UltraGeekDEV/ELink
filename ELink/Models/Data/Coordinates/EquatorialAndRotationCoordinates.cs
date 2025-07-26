using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Models.Data.Coordinates
{
    public struct EquatorialAndRotationCoordinates
    {
        public double Ra { get; set; }
        public double Dec { get; set; }
        public double R { get; set; }
        public EquatorialAndRotationCoordinates()
        {
        }

        public static implicit operator EquatorialCoordiantes(EquatorialAndRotationCoordinates EQR)
        {
            return new EquatorialAndRotationCoordinates { Ra = EQR.Ra, Dec = EQR.Dec};
        }
    }
}
