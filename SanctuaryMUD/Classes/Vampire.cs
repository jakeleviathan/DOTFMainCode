using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Vampire : Traveler
    {
        public override string Name => "Vampire";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 0 },
            { "Dexterity", 0 },
            { "Intelligence", 2 },
            { "Wisdom", 0 },
            { "Charisma", 0 },
            { "Constitution", 0 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Vampirism", 3 },
            { "Piercing", 1 },
            { "Blood Magic", 1 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Blood Drain",
            "Spirit Recall",
            "Bite"
        };

        public override bool CanBecomeClass(Soul soul)
        {
          //  Console.WriteLine("Checking if soul can become Vampire...");
            // Check if the soul meets the requirements for becoming a Wizard
            return soul.Level >= 1 && soul.Skills["Vampirism"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Vampire class to the soul
           // Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
            
        }
    }
}
