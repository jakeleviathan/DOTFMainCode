using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanctuaryMUD
{
    public enum RaceType
    {
        Human,
        MoonElf,
        Gorgon,
        Nightshade,
        Gnome,
        Melodian,
        ManaElf,
        Arcturian,
        Aurizari,
        Siren,
        Unknown
    }

    public class Race
    {
        public int Id { get; set; }
        public RaceType RaceType { get; set; }
        
        public string RaceDescription { get; set; }
        public int Str { get; set; }
        public int Dex { get; set; }
        public int Con { get; set; }
        public int Int { get; set; }
        public int Wis { get; set; }
        public int Cha { get; set; }
        public string SpecialAbilityName { get; set; }
        public string SpecialAbilityDescription { get; set; }
        public string RaceName { get; set; }
        public Dictionary<string, int> BaseStats { get; set; }
        public string SpecialAbility { get; set; }
        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }

        public Race(int id, RaceType raceType, string raceDescription, int str, int dex, int con, int intel, int wis, int cha,
            string specialAbilityName, string specialAbilityDescription, string raceName, Dictionary<string, int> baseStats, string specialAbility, int maxHealth, int maxMana)
        {
            Id = id;
            RaceType = raceType;
            RaceDescription = raceDescription;
            Str = str;
            Dex = dex;
            Con = con;
            Int = intel;
            Wis = wis;
            Cha = cha;
            SpecialAbilityName = specialAbilityName;
            SpecialAbilityDescription = specialAbilityDescription;
            RaceName = raceName;
            BaseStats = baseStats;
            SpecialAbility = specialAbility;
            MaxHealth = maxHealth;
            MaxMana = maxMana;
        }
    }

    public static class Races
    {
        public static List<Race> AllRaces = new List<Race>
        {
            new Race(1, RaceType.Human, "The Humans of Earth are a hardy and adaptable race, with a strong will to survive and a deep curiosity about the world around them. They are often driven by a sense of adventure and a desire to explore the unknown, which makes them well-suited for the challenges of Magnos.\r\n\r\nDespite being bound to the limitations of the text-based portal, Humans remain resourceful and creative in their approach to problem-solving. They are often the first to come up with new strategies and tactics for navigating the dangerous and unpredictable landscape of Magnos.\r\n\r\nHumans are also known for their diverse range of skills and abilities. They possess a natural aptitude for a wide variety of trades and professions, from farming and crafting to magic and combat. This versatility makes them a valuable asset in any situation, as they are able to adapt to changing circumstances and challenges.\r\n\r\nAbove all, Humans are characterized by their resilience and adaptability. They are able to thrive in even the most challenging of environments, and are not easily discouraged by setbacks or obstacles. This spirit of determination and perseverance makes them a force to be reckoned with in the world of Magnos.\r\n\r\n",
                1, 1, 1, 1, 1, 1, "Inspire",
                "Grants a temporary boost to a single ally's strength and dexterity stats", "Human",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Inspire",
                20,
                20 
            ),
            new Race(2, RaceType.MoonElf, "The moon elves are a small, hardworking community from the planet Qorolox, a world still in the early stages of development. Despite their small size, the moon elves are known throughout the region for their ingenuity, their dedication to their work, and their unwavering commitment to their community.\r\n\r\nThe history of the moon elves is one of perseverance and determination. Though they live in a world that is still reliant on antiquated forms of energy like coal, they never lose sight of their goals. They work tirelessly to improve their own lives and the lives of those around them, always striving to find new ways to harness the power of the world around them.\r\n\r\n",0, 1, 0, 1, 0, 1, "Moonlit Dodge",
                "Grants passive evasion, giving a small chance to avoiding enemy attacks", "Moon Elf",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Moonlit Dodge",
                20,
                20 
            ),
            new Race(3, RaceType.Gorgon, "A Gorgon is a cosmic creation entity that is both beautiful and terrifying. They are humanoid in shape, standing tall on two legs like humans, but their appearance is far from human. They have goat hooves instead of feet, and their skin is a dark, mottled gray. Their bodies are lean and sinewy, with rippling muscles that speak of raw power.\r\n\r\nThe most striking feature of a Gorgon is their hair. Instead of human hair, they have a mass of writhing snakes that slither and writhe around their head. These snakes are a symbol of their malevolent nature and are used as weapons to strike fear into the hearts of their enemies.",2, 0, 1, -1, 0, 0, "Petrify", "Temporarily immobilizes an enemy", "Gorgon",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Petrify",
                20,
                20 
            ),
            new Race(4, RaceType.Nightshade, "The Nightshades are a rare and mystical race, born from the dark occult magic experimentation that took place in Gal’Vae, City of the Dead. They embody the strengths and weaknesses of both elves and undead, and possess a unique combination of physical prowess and magical abilities that make them a force to be reckoned with.\r\n\r\nTheir skin is pale and cold to the touch, and their movements are slow and deliberate. Their eyes, sunken and dark, are filled with an otherworldly power that can strike fear into the hearts of their enemies. \r\n\r\n",0, 2, -1, 0, 0, 0, "Shadowstep",
                "Teleports to a nearby location, gaining a temporary boost to attack speed", "Nightshade",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Shadowstep",
                19,
                20 
            ),
            new Race(5, RaceType.Gnome, "Gnomes are known for their incredible fashion sense and their ability to create cutting-edge designs that are both practical and stylish. They are often seen wearing sleek, futuristic clothing and accessories that make them stand out in any crowd. Their love of all things stylish and trendy is matched only by their obsession with entertaining and being the center of attention.\r\n\r\nGnomes are natural entertainers and clowns. They love to make people laugh and are known for their quick wit and sharp tongues. They are often seen performing on stage or on street corners, entertaining crowds with their stand-up comedy and acrobatic feats.\r\n\r\n",-1, 2, 1, 2, 0, 0, "Tinker",
                "Escapes a sticky situation, dropping aggro on anything attacking you.", "Gnome",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Escape",
                20,
                20 
            ),
            new Race(6, RaceType.Melodian, "The Melodians are a mythical race of cosmic beings that emerged from the very essence of music itself. They are enigmatic creatures, known for their otherworldly abilities and their deep connection to the universe. According to ancient lore, the Melodians were born at the very moment when the universe first began to sing.\r\n\r\nThe Melodians are experts in the art of music, and they possess the power to manipulate sound and rhythm in ways that are beyond human comprehension. They can create intricate melodies and harmonies that can soothe the soul or drive a person to madness. They are also skilled in the art of divination, and they use their mystical powers to guide and protect those who seek their wisdom.",0, -1, 1, 2, 2, 1, "Harmonic Chorus",
                "Heals everyone in the room and buffs their intelligence temporarily", "Melodian",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Harmonic Chorus",
                18,
                22 
            ),
            new Race(7, RaceType.ManaElf, "Mana Elves are primarily found in the cavernous city of Xuolia, located deep beneath the ground. The city is a marvel of magical engineering, with towering spires and sprawling tunnels that stretch for miles in every direction. Mana Elves are fiercely loyal to their city and the magical traditions that have been passed down through generations, and they will stop at nothing to protect their home and their way of life.\r\n\r\nWhile Mana Elves are known for their magical prowess, they are also skilled warriors and craftsmen. Many Mana Elves specialize in the use of enchanted weapons and armor, which they imbue with powerful spells to give them an edge in combat. Others are skilled artisans, crafting intricate works of art and jewelry using rare and exotic materials infused with magic.\r\n\r\n",-1, 2, 1, 2, 2, 1, "Mana Regeneration",
                "Passively increases mana regeneration by an extra amount", "Mana Elf",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Magical Blood",
                15,
                30 
            ),
            new Race(8, RaceType.Arcturian, "Arcturians are a proud and industrious people, known for their remarkable intelligence and advanced technological capabilities. With a natural inclination towards science and engineering, they have developed some of the most advanced technologies in the known universe.\r\n\r\nArcturians are stern and businesslike by nature, with a no-nonsense attitude that can be intimidating to outsiders. They value efficiency and practicality above all else, and are driven to succeed in their endeavors.\r\n\r\nDespite their serious demeanor, Arcturians also have a strong sense of community and loyalty. They take great pride in their accomplishments, and are quick to defend their people and their way of life.",1, 0, 2, -1, 1, 1, "Frost Shield",
                "Grants a temporary cold damage shield", "Arcturian",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Cold Fusion Shield",
                20,
                21 
            ),
            new Race(9, RaceType.Aurizari, "The Aurizari are a proud and ancient tribe that once built the magnificent jade and gold temples that still stand in Albarn to this day. They worship gold as a symbol of wealth, power, and prosperity, and their entire society revolves around the acquisition and use of this precious metal.\r\n\r\nThe Aurizari draw their magical power from alchemy, specifically the magical properties of gold and mystical blackmetal. They are skilled alchemists and metallurgists, capable of crafting powerful magical artifacts that are prized throughout the land.\r\n\r\nDespite their love of gold, the Aurizari are not greedy or selfish. They believe that the acquisition of wealth and power should be shared among all members of their society, and they work together to ensure that everyone has access to the resources they need.\r\n\r\n",0, 0, 0, 4, -2, 0, "Enlightenment",
                "Passively increases the amount of Astrals dropped from defeated enemies", "Aurizari",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Astral Endowment",
                19,
                22 
            ),
            new Race(10, RaceType.Siren, "Sirens are a highly organized and sophisticated society. They are ruled by a powerful queen who maintains strict control over her subjects. The queen is chosen through a complex ritual involving the singing of the entire population, and her word is law.\r\n\r\nThe Sirens are an all-female race, but males can choose to become Siren thralls if their character is male. These thralls are subservient to the queen and other female Sirens, but they are allowed to remain in the society and serve as protectors or servants.\r\n\r\n",-1, 0, 1, 2, 1, 3, "Sirens Call",
                "Charms all enemies in the room, decreasing their attack speed", "Siren",
                new Dictionary<string, int>
                {
                    { "STR", 1 },
                    { "DEX", 1 },
                    { "CON", 1 },
                    { "INT", 1 },
                    { "WIS", 1 },
                    { "CHA", 1 }
                },
                "Charm",
                20,
                20 
            ),
        };

        public static int GetRaceId(RaceType raceType)
        {
            return AllRaces.First(race => race.RaceType == raceType).Id;
        }
        
        public static Race GetRace(RaceType raceType)
        {
            return AllRaces.FirstOrDefault(race => race.RaceType == raceType);
        }
        public static List<Race> GetAllRaces()
        {
            return AllRaces;
        }
 
    }
}
