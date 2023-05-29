using System;
using System.Collections.Generic;
using System.Linq;
using SanctuaryMUD;

namespace SanctuaryMUD.Classes
{
    public class Traveler : Soul.PlayerClass
    {
            public override string Name => "Traveler";

        public override Dictionary<string, int> Attributes => new Dictionary<string, int>
        {
            { "Strength", 1 },
            { "Dexterity", 1 },
            { "Intelligence", 1 },
            { "Wisdom", 1 },
            { "Charisma", 1 },
            { "Constitution", 1 }
        };

        public override Dictionary<string, int> Skills => new Dictionary<string, int>
        {
            { "Hand to Hand", 1 },
            { "Exploration", 1 }
        };

        public override List<string> Abilities => new List<string>
        {
            "Spirit Recall",
            "Blessing",
            "Mindbright"
        };

        public override bool CanBecomeClass(Soul soul)
        {
            // Check if the soul meets the requirements for becoming a Traveler
            return soul.Level >= 1 && soul.Skills.Values.All(skill => skill.CurrentValue == 0);
        }

        public override void ApplyClass(Soul soul)
        {
            // Apply the Traveler class to the soul
          //  Console.WriteLine($"Applying {Name} class to {soul.Name}");
            soul.CurrentClass = new Traveler();
        }
    }
}