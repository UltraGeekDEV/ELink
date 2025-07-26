using ELink.Interfaces.Utils;
using ELink.Models.Data.Coordinates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Interfaces.Hardware
{
    public interface IMount
    {
        public void GoTo(ICoordinates coordinates);
        public void Sync(ICoordinates coordinates);
        public void SyncGuide(ICoordinates coordinates);
        public void CreateMountModel();
        public void Abort();
        public void TogglePark();
        public void Home();
    }
}
