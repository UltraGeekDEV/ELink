using ELink.Interfaces.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Models.Data.Image
{
    public class ELinkImage
    {
        public Dictionary<string, FITSHeaderItem> FitsHeader { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public ImageType Type { get; set; }
        public float[] Data { get; set; }
        public string Filter { get; set; }
        public ELinkImage()
        {
            FitsHeader = new Dictionary<string, FITSHeaderItem>();
        }
    }
}
