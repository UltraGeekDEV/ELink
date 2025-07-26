using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Interfaces.Hardware
{
    public interface ICamera
    {
        public void SetExposureLegth(float time);
        public void SetGain(int gain);
        public int TakeFrame();
    }
}
