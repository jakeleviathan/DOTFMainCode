using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Etherealist : Traveler
    {
        public override string Name => "Etherealism";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 0 },
            { "Dexterity", 1 },
            { "Intelligence", 0 },
            { "Wisdom", 0 },
            { "Charisma", 1 },
            { "Constitution", 0 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Etherealism", 8 },
            { "Exploration", 5 },
            { "Teleportation", 3 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Ghostwalk",
            "Spirit Recall",
            "Blessing",
            "Cosmic Essence",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
           // Console.WriteLine("Checking if soul can become Ghostwalker...");
            // Check if the soul meets the requirements for becoming a Ghostwalker
            return soul.Level >= 1 && soul.Skills["Etherealism"].CurrentValue >= 3;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Ghostwalker class to the soul
         //   Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
