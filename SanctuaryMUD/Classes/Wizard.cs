using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Wizard : Traveler
    {
        public override string Name => "Wizard";

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
            { "Arcanism", 3 },
            { "Blunt", 1 },
            { "Teleportation", 1 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Coldfire",
            "Spirit Recall",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
          //  Console.WriteLine("Checking if soul can become Wizard...");
            // Check if the soul meets the requirements for becoming a Wizard
            return soul.Level >= 1 && soul.Skills["Arcanism"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Rogue class to the soul
           // Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
