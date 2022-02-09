using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoE_ExperienceFetcher.Json_Objects
{
    internal class LadderEntry
    {
        public int Rank { get; set; }
        public bool Dead { get; set; }
        public Character Character { get; set; }
        public Account Account { get; set; }

        public override string ToString()
        {
            return $"Account: {Account.Name} Character: {Character.Name} ID: {Character.Id}\nRank: {this.Rank} Level: {Character.Level} Experience: {Character.Experience}";
        }

    }

    internal class Character
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Class { get; set; }
        public uint Experience { get; set; }

    }

    internal class Account
    {
        public string Name { get; set; }
    }
}
