using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Priest : Traveler
    {
        public override string Name => "Priest";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 0 },
            { "Dexterity", 0 },
            { "Intelligence", 0 },
            { "Wisdom", 1 },
            { "Charisma", 0 },
            { "Constitution", 1 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Faith", 8 },
            { "Occultism", 5 },
            { "White Magic", 3 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Divine Blessing",
            "Spirit Recall",
            "Blessing",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
          //  Console.WriteLine("Checking if soul can become Priest...");
            // Check if the soul meets the requirements for becoming a Priest
            return soul.Level >= 1 && soul.Skills["Faith"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Priest class to the soul
         //   Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
