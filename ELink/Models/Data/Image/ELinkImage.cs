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
        public int width { get; set; }
        public int height { get; set; }
        public ImageType type { get; set; }
        public float[] data { get; set; }
        public string filter { get; set; }
    }
    public enum ImageType
    {
        Mono,
        Color,
        PseudoMono
    }
}
