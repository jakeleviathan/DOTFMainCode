using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SanctuaryMUD
{
    [Serializable]
    public class Item
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Slot { get; set; }
        public int SlotNumber { get; set; }
       
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Wis { get; set; }
    public int Int { get; set; }
    public int Con { get; set; }
    public int Cha { get; set; }
        public string Type { get; set; }
        public string Alias { get; set; }
        public bool IsFashion { get; set; }
        public string Description { get; set; }
        public Item() { }

        public Item Clone()
        {
            return new Item
            {
                ID = ID,
                Name = Name,
                Slot = Slot,
                SlotNumber = SlotNumber,
                Str = Str,
                Dex = Dex,
                Wis = Wis,
                Int = Int,
                Con = Con,
                Cha = Cha,
                Type = Type,
                Alias = Alias,
                IsFashion = IsFashion,
                Description = Description
            };
        }
        public string GetDisplayName()
        {
            if (IsFashion)
            {
                return $"\u001b[36m{Name}\u001b[0m (Fashion)";
            }
            else
            {
                return $"\u001b[95m{Name}\u001b[0m";
            }
        }
    }
}
