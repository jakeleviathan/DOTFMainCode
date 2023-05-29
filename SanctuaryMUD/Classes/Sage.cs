using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Sage : Traveler
    {
        public override string Name => "Sage";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 0 },
            { "Dexterity", 0 },
            { "Intelligence", 1 },
            { "Wisdom", 1 },
            { "Charisma", 0 },
            { "Constitution", 0 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Arcanism", 3 },
            { "Sagacity", 5 },
            { "Blue Magic", 3 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Cosmic Essence",
            "Spirit Recall",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
        //    Console.WriteLine("Checking if soul can become Sage...");
            // Check if the soul meets the requirements for becoming a Sage
            return soul.Level >= 1 && soul.Skills["Sagacity"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Sage class to the soul
        //    Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
