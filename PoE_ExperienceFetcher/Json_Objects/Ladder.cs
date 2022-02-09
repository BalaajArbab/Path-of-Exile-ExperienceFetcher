using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoE_ExperienceFetcher.Json_Objects
{
    internal class Ladder
    {
        public int Total { get; set; }
        public string Cached_Since { get; set; }

        public LadderEntry[] Entries { get; set; }

        public bool ContainsId(string Id)
        {
            foreach (LadderEntry l in Entries)
            {
                if (l.Character.Id == Id) return true;
            }

            return false;
        }
    }
}
