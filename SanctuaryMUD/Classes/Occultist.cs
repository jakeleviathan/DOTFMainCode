using System;
using System.Collections.Generic;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Occultist : Traveler
    {
        public override string Name => "Occultist";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 0 },
            { "Dexterity", 0 },
            { "Intelligence", 1 },
            { "Wisdom", 1 },
            { "Charisma", 0 },
            { "Constitution", 1 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Occultism", 5 },
            { "Black Magic", 5 },
            { "Blood Magic", 5 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Cosmic Essence",
            "Spirit Recall",
            "Dark Pact",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
          //  Console.WriteLine("Checking if soul can become Occultist...");
            // Check if the soul meets the requirements for becoming a Occultist
            return soul.Level >= 1 && soul.Skills["Occultism"].CurrentValue >= 1;
        }


        public override void ApplyClass(Soul soul)
        {
            // Apply the Sage class to the soul
        //    Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = this;
        }
    }
}
