using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Swordsman : Traveler
    {
        public override string Name => "Swordsman";

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
            { "Hand to Hand", 3 },
            { "Slashing", 1 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Spirit Recall"
        };

        public override bool CanBecomeClass(Soul soul)
        {
           // Console.WriteLine("Checking if soul can become Swordsman...");
            // Check if the soul meets the requirements for becoming a Swordsman
            return soul.Level >= 1 && soul.Skills["Slashing"].CurrentValue >= 3;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Swordsman class to the soul
          //  Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
