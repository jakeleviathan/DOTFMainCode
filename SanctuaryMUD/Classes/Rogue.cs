using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Rogue : Traveler
    {
        public override string Name => "Rogue";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 1 },
            { "Dexterity", 1 },
            { "Intelligence", 0 },
            { "Wisdom", 0 },
            { "Charisma", 1 },
            { "Constitution", 0 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Stealth", 5 },
            { "Selling", 1 },
            { "Piercing", 3 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Improved Hide",
            "Spirit Recall",
            "Blessing",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
           // Console.WriteLine("Checking if soul can become Rogue...");
            // Check if the soul meets the requirements for becoming a Rogue
            return soul.Level >= 1 && soul.Skills["Stealth"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Rogue class to the soul
       //     Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
