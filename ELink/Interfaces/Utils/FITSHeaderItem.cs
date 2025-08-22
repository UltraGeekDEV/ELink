using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELink.Interfaces.Utils
{
    public class FITSHeaderItem
    {
        public FITSHeaderItem(string block)
        {
            var items = block.Split('=', '/');
            key = items[0].Trim();
            value = items[1].Trim();
            comment = items[2].Trim();
        }

        public string key {  get; set; }
        public string value { get; set; }
        public string comment { get; set; }

        public string GetFitsHeaderItem()
        {
            string ret = "";
            ret += key.PadRight(8);
            ret += "= ";
            if (value.Contains('\'') || value.Contains('"'))
            {
                ret += value.PadRight(20);
            }
            else
            {
                ret += value.PadLeft(20);
            }
            ret += " / ";
            ret += comment;
            ret = ret.PadRight(80);
            return ret;
        }
    }
}
