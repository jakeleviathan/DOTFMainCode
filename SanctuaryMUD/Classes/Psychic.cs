using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Psychic : Traveler
    {
        public override string Name => "Psychic";

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
            "Telepathy"
        };

        public override bool CanBecomeClass(Soul soul)
        {
           // Console.WriteLine("Checking if soul can become Psychic...");
            // Check if the soul meets the requirements for becoming a Psychic
            return soul.Level >= 1 && soul.Skills["Psionics"].CurrentValue >= 20;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Psychic class to the soul
       //     Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
