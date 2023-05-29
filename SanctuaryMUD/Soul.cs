using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Data.SQLite;
using Newtonsoft.Json;
using SanctuaryMUD.Classes;


namespace SanctuaryMUD
{
    public class Soul : Entity
    {
        public string Name { get; set; }
        public string PasswordHash { get; set; }
        public Race Race { get; set; }
        public int Age { get; set; }
        public Dictionary<int, Item> Equipment { get; set; } = new Dictionary<int, Item>();
        public event EventHandler PlayerLeavingRoom;
        public List<string> Wounds { get; set; }    
        public Dictionary<string, Item> Fashion { get; set; } = new Dictionary<string, Item>();
        public Dictionary<int, Item> Inventory { get; set; }
        public Room CurrentRoom { get; set; }
        public DateTime BiteCooldown { get; set; }
        public int MaxMovementPoints { get; set; }
        public int MovementPoints { get; set; }
        public DateTime LastPrayerTime { get; set; }
        public string Salt { get; set; }
        public int RaceId { get; set; }
        public int CurrentHealth { get; set; } 
        
        public int MaxHealth { get; set; }
        public int CurrentMana { get; set; } 
        public int MaxMana { get; set; }
        public int CurrentExperience { get; set; } 
        public int MaxExperience { get; set; } 
        public int Level { get; set; }
        public int CurrentRoomId { get; set; }
        public bool IsGM { get; set; }
        public bool IsSitting { get; set; }
        public ClientConnection Connection { get; set; }
        public DateTime LastSeen { get; set; }
        public int Astrals { get; set; }
        public int VoidAstrals { get; set; }
        public List<string> HackingTools { get; private set; }
        public string DecryptionMethod { get; private set; }
        public int Impcoins { get; set; }
        public Timer ImpcoinTimer { get; set; }
        public PlayerClass CurrentClass { get; set; }
        public Dictionary<string, int> Stats { get; set; }
        public List<string> Abilities { get; set; }
        public Dictionary<string, Skill> Skills { get; set; }
        public Dictionary<string, int> Attributes { get; set; }
        public Dictionary<string, FriendList> Friends { get; set; }
        public bool IsAFK { get; set; }
        public bool IsIllusion { get; set; }
        public string IllusionObject { get; set; }
        public bool IsWind { get; set; }
        public bool CosmicEssenceActive { get; set; } = false;
        private const int BASE_ATTACK_DELAY_TIME = 15; // Base delay time in seconds for a soul's attack
        public List<string> ActiveEffects { get; set; } = new List<string>();
        public DateTime LastLogin { get; set; }
        public bool IsHidden { get; set; }
        public string Bio { get; set; }
        public bool IsAnonymous { get; set; } = false;
        public Dictionary<string, DateTime> ActiveEffectExpirationTimes { get; set; } = new Dictionary<string, DateTime>();
        public DateTime SalveLastCastTime { get; set; }
        public int RecallRoom { get; set; }
        public string ClassName { get; set; }
        public Dictionary<string, bool> ClaimedGifts { get; set; }
        public DateTime LastPhilosophySkillUpTime { get; set; }
        public DateTime ColdfireLastCastTime { get; set; }
        public Group Group { get; set; }
        public Group GroupInvite { get; set; }
        private DateTime _nextAttackMessage = DateTime.Now;
        public int HealthRegenModifier { get; set; }
        public int ManaRegenModifier { get; set; }
        public bool IsPetrified { get; set; }
        public DateTime? HackingStartTime { get; set; }
        public int? HackedVendingMachineId { get; set; }

        public List<string> InspectedObjects { get; set; }
        public const int maxInventorySlots = 20;
        public bool IsMoonElf(Soul soul)
        {
            return soul.Race.RaceName.Equals("moon elf", StringComparison.OrdinalIgnoreCase);
        }
        public DateTime LastSkillGainTime { get; set; }
        private static List<string> objectList = new List<string> {
         "altar", "candle", "besom", "ritual chair", "occult table", "grimoire", "wand", "lantern", "scrying mirror", "pentagram rug",
"sigil", "hourglass", "chalice", "potted mandrake", "dreamcatcher", "tarot cloth", "occult painting", "statue of deity", "spirit board", "secret door",
"stained glass window", "skeleton key", "hidden bookcase", "goblet", "ceremonial plate", "athame", "ceremonial spoon", "boline", "cloth napkin", "enchanted cushion",
"prayer mat", "ritual towel", "black soap", "moonwater", "toothbrush", "charcoal toothpaste", "ceremonial razor", "bone comb", "hairbrush", "tissue",
"hand mirror", "essential oil", "ritual makeup", "cauldron", "bathtub", "stone basin", "chamber pot", "towel rack", "lampshade", "bedspread",
"nightstand", "candleholder", "astrological chart", "writing desk", "crystal ball", "runes", "inkwell", "parchment", "desk",
"chair", "sealing wax", "seal", "notepad", "chest", "moon calendar", "trash can", "mop", "bucket", "sponge",
"herbal soap", "drying rack", "fireplace", "clay oven", "stove", "icebox", "pottery", "churn", "mortar and pestle",
"whetstone", "grindstone", "coffee grinder", "kettle", "wine press", "mead", "food processor", "knife set", "cutting board", "pot", "pan",
"baking dish", "measuring cup", "measuring spoon", "spatula", "whisk", "ladle", "tongs", "corkscrew", "bottle opener",
"lock", "potion", "spellbook", "magic wand", "amulet", "cloak", "enchanted scroll", "mysterious artifact", "staff",
"ring", "magic sword", "bow", "crossbow", "quiver", "boots", "helmet", "shield", "armor", "gloves",
"greaves", "belt", "backpack", "torch", "flint and steel", "waterskin", "tent", "bedroll", "rope",
"grappling hook", "pickaxe", "shovel", "hammer", "crowbar", "fishing rod", "fishhook", "compass", "sextant", "spyglass",
"herbs", "pouch", "flask", "coin purse", "lockpick", "familiar", "crystal", "talisman", "drum", "flute",
"harp", "lute", "lyre", "horn", "whistle", "bagpipes", "pennywhistle", "bone flute", "xylophone", "tambourine",
"alchemy set", "bellows", "anvil", "tongs", "forging hammer", "smelting pot", "chisel", "goggles", "apron",
"caltrops", "trap", "manacles", "poison", "dagger", "shortsword", "mace", "morning star", "holo-display", "energy cell", "teleporter", "laser pistol", "force field", "droid", "spaceship", "hologram projector", "neural implant", "warp drive",
"nanobot", "cybernetic arm", "cybernetic leg", "power armor", "gravity boots", "plasma rifle", "energy sword", "quantum computer", "exo-suit", "force shield",
"personal drone", "alien artifact", "stasis chamber", "time machine", "hoverboard", "fusion reactor", "android", "orbital satellite", "particle accelerator", "emp device",
"invisibility cloak", "matter replicator", "universal translator", "bio-scanner", "wormhole generator", "portal gun", "med-kit", "jetpack", "sonic screwdriver", "zero-point energy module",
"anti-gravity device", "cloaking device", "synthetic heart", "bionic eye", "cybernetic spine", "regeneration chamber", "memory chip", "brain-computer interface", "virtual reality headset", "interstellar telescope",
"solar sail", "neutrino detector", "holographic storage device", "subspace communicator", "energy converter", "light saber", "ray gun", "photon grenade", "stellar map", "gyroscopic stabilizer",
"arc reactor", "tachyon emitter", "plasma cannon", "cryogenic capsule", "telepathic amplifier", "molecular assembler", "ionic blaster", "wrist-mounted computer", "multi-tool", "vase", "broom", "table", "book", "quill", "oil lamp", "mirror", "rug",
"picture", "teapot", "plant", "pillow", "blanket", "painting", "sculpture", "scroll", "door",
"window", "key", "bookcase", "cup", "plate", "fork", "spoon", "knife", "napkin", "cushion",
"mat", "towel", "soap", "herbal shampoo", "toothpaste", "razor", "comb", "hand mirror", "perfume", "cosmetics", "washbasin", "sink", "map", "game board", "abacus",
"calendar", "soapstone", "oven", "icebox", "pottery", "measuring cup", "measuring spoon",
"lock", "potion", "spellbook", "magic wand", "amulet", "cloak", "enchanted scroll", "mysterious artifact", "crystal ball", "staff",
"ring", "magic sword", "bow", "crossbow", "quiver", "boots", "helmet", "shield", "armor", "gloves",
"greaves", "belt", "backpack", "lantern", "torch", "flint and steel", "waterskin", "tent", "bedroll", "rope",
"grappling hook", "pickaxe", "shovel", "hammer", "crowbar", "fishing rod", "fishhook", "compass", "sextant", "spyglass",
"herbs", "pouch", "flask", "coin purse", "lockpick", "familiar", "crystal", "talisman", "drum", "flute",
"harp", "lute", "lyre", "horn", "whistle", "bagpipes", "pennywhistle", "bone flute"
        };
        private static List<string> plantList = new List<string> {
            "aloe vera", "azalea", "bamboo", "basil", "bonsai", "Xuolia fern", "cactus", "chives", "daffodil", "daisy",
            "dandelion", "Humid Plains ivy", "ficus", "fiddle-leaf fig", "gardenia", "geranium", "hibiscus", "hosta", "hydrangea", "impatiens",
            "iris", "jade plant", "jasmine", "lavender", "lilac", "lily", "marigold", "mint", "monstera", "morning glory",
            "moss", "orchid", "palm", "pansy", "peace lily", "peony", "petunia", "philodendron", "pothos", "rose",
            "rosemary", "rubber plant", "sage", "snake plant", "snapdragon", "spider plant", "succulent", "sunflower", "tulip", "Qorolox flytrap",
            "violet", "wisteria", "yucca", "zinnia",
            "anthurium", "areca palm", "asparagus fern", "begonia", "bird of paradise", "bird's nest fern", "bleeding heart", "calathea", "camellia", "coleus",
            "columbine", "coral bells", "corn plant", "croton", "cyclamen", "dahlia", "dieffenbachia", "dracaena", "echinacea", "elephant ear",
            "eucalyptus", "forsythia", "foxtail fern", "ginkgo", "golden pothos", "hellebore", "hen and chicks", "kalanchoe", "lantana", "lupine",
            "magnolia", "nasturtium", "nerve plant", "oleander", "oxalis", "paperwhite", "parlor palm", "passionflower", "periwinkle", "plumeria",
            "poinsettia", "ponytail palm", "prayer plant", "primrose", "red hot poker", "rhododendron", "sansevieria", "schefflera", "shasta daisy", "silver astral",
            "spathiphyllum", "spathoglottis", "stargazer lily", "stephanotis", "string of pearls", "swiss cheese plant", "thyme", "tiger lily", "umbrella tree", "weeping fig"
        };

        //isalive flag
        public bool IsAlive { get; set; } = true;

        //isattacking flag
        public bool IsAttacking { get; set; } = false;

        //currenttarget
        public Entity CurrentTarget { get; set; }
        //canskillup flag
        public bool CanSkillUp { get; set; } = true;
        public List<string> LearnedAbilities { get; set; } = new List<string>();
        private readonly Server _server;
        private string currentClass; 
        // Check if the player has all the required items in their inventory
        private bool HasItems(List<Item> requiredItems)
        {
            var itemCounts = new Dictionary<string, int>();

            foreach (var item in requiredItems)
            {
                if (itemCounts.ContainsKey(item.Name))
                {
                    itemCounts[item.Name]++;
                }
                else
                {
                    itemCounts[item.Name] = 1;
                }
            }

            foreach (var kvp in Inventory)
            {
                if (itemCounts.ContainsKey(kvp.Value.Name))
                {
                    itemCounts[kvp.Value.Name]--;
                    if (itemCounts[kvp.Value.Name] <= 0)
                    {
                        itemCounts.Remove(kvp.Value.Name);
                    }
                }
            }

            return itemCounts.Count == 0;
        }





        public Soul(Server server, string name, string password, Race race, string salt, int age, int maxHealth, int maxMana,
            int currentHealth, int currentMana, int currentExperience, int level, int currentRoomId, bool isGM,
            DateTime lastLogin, string bio, Dictionary<string, Skill> skills, int recallRoom, int astrals )
        {
            Name = name;
            PasswordHash = ComputeHash(password, salt);
            Race = race;
            Salt = salt;
            Age = age;
            Fashion = new Dictionary<string, Item>();
            CurrentRoom = null;
            MaxHealth = maxHealth;
            CurrentHealth = currentHealth;
            MaxMana = maxMana;
            CurrentMana = currentMana;
            CurrentExperience = 0;
            MaxExperience = 100;
            Level = level;
            CurrentRoomId = currentRoomId;
            IsGM = isGM;
            Astrals = astrals;
            VoidAstrals = 0;
            Impcoins = 0;
            LastLogin = lastLogin;
            Bio = bio;
            RecallRoom = 7;
            Stats = new Dictionary<string, int>();
            Abilities = new List<string>();
            Inventory = new Dictionary<int, Item>();
            ClaimedGifts = new Dictionary<string, bool>();
            IsAttacking = false;
            LearnedAbilities = new List<string>();
            _server = server;
            currentClass = "Traveler";
            InspectedObjects = new List<string>();
            LastPrayerTime = DateTime.MinValue;
            Wounds = new List<string>();
            MaxMovementPoints = 10; // or any other initial value
            MovementPoints = MaxMovementPoints;
            HackingTools = new List<string>();

            // Initialize the Equipment dictionary with default values
            for (int i = 1; i <= 19; i++)
            {
                Equipment[i] = null;
            }

            Skills = skills;

            // Add the racial stats to the player's Attributes
            Attributes = new Dictionary<string, int>
            {
                { "Strength", race.BaseStats["STR"] },
                { "Dexterity", race.BaseStats["DEX"] },
                { "Intelligence", race.BaseStats["INT"] },
                { "Wisdom", race.BaseStats["WIS"] },
                { "Charisma", race.BaseStats["CHA"] },
                { "Constitution", race.BaseStats["CON"] }
            };
            Friends = new Dictionary<string, FriendList>();
            


        }

        [Serializable]
        public class Slot
        {
            public int SlotNumber { get; set; }
            public string SlotName { get; set; }

            public Slot(int slotNumber, string slotName)
            {
                SlotNumber = slotNumber;
                SlotName = slotName;
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                Slot otherSlot = (Slot)obj;
                return SlotNumber == otherSlot.SlotNumber && SlotName == otherSlot.SlotName;
            }

            public override int GetHashCode()
            {
                return SlotNumber.GetHashCode() ^ SlotName.GetHashCode();
            }
        }
        public void ChooseHackingTools(params string[] tools)
        {
            HackingTools.Clear();
            HackingTools.AddRange(tools);
        }

        public void ChooseDecryptionMethod(string method)
        {
            DecryptionMethod = method;
        }
        public void SubscribeToPlayerLeavingRoom(EventHandler eventHandler)
        {
            PlayerLeavingRoom += eventHandler;
        }

        public void UnsubscribeFromPlayerLeavingRoom(EventHandler eventHandler)
        {
            PlayerLeavingRoom -= eventHandler;
        }
    
        public void AddFriend(string friendName)
        {
            if (Friends.ContainsKey(friendName))
            {
                Connection.SendMessage($"{friendName} is already your friend.");
                return;
            }

            Friends[friendName] = new FriendList();
            Connection.SendMessage($"You have sent a friend request to {friendName}.");
        }
        public void RaisePlayerLeavingRoomEvent()
        {
            PlayerLeavingRoom?.Invoke(this, EventArgs.Empty);
        }

        public int GetTotalAttributeValue(string attribute)
        {
            int baseValue = Attributes[attribute];
            int equipmentValue = 0;

            foreach (var slotItem in Equipment)
            {
                if (slotItem.Value != null)
                {
                    switch (attribute)
                    {
                        case "Strength":
                            equipmentValue += slotItem.Value.Str;
                            break;
                        case "Dexterity":
                            equipmentValue += slotItem.Value.Dex;
                            break;
                        case "Wisdom":
                            equipmentValue += slotItem.Value.Wis;
                            break;
                        case "Intelligence":
                            equipmentValue += slotItem.Value.Int;
                            break;
                        case "Constitution":
                            equipmentValue += slotItem.Value.Con;
                            break;
                        case "Charisma":
                            equipmentValue += slotItem.Value.Cha;
                            break;
                    }
                }
            }

            // Add the "inspire" effect bonus if active
            int inspireEffectValue = 0;
            if (ActiveEffects.Contains("inspire") && (attribute == "Strength" || attribute == "Dexterity"))
            {
                inspireEffectValue = 1;
            }

            // Add the "shadowstep" effect bonus if active
            int shadowstepEffectValue = 0;
            if (ActiveEffects.Contains("Shadowstep") && attribute == "Dexterity")
            {
                shadowstepEffectValue = 2;
            }

            return baseValue + equipmentValue + inspireEffectValue + shadowstepEffectValue;
        }



        public int GetTotalMaxHealth()
        {
            int baseMaxHealth = MaxHealth;

            // Add the "Sunlight" effect bonus if active
            int sunlightEffectValue = 0;
            if (ActiveEffects.Contains("sunlight"))
            {
                sunlightEffectValue = (int)(baseMaxHealth * 0.1);
            }
            else
            {
                sunlightEffectValue = 0;
            }
            // Add the "Cloudy Skies" effect bonus if active
            int cloudySkiesEffectValue = 0;
            if (ActiveEffects.Contains("cloudy skies"))
            {
                cloudySkiesEffectValue = (int)(baseMaxHealth * 0.05);
            }
            int newMaxHealth = baseMaxHealth + sunlightEffectValue + cloudySkiesEffectValue;

            // Update the current health to be appropriate to the new max health
            if (CurrentHealth > newMaxHealth)
            {
                CurrentHealth = newMaxHealth;
            }

            return newMaxHealth;
        }

        public int GetTotalMaxMana()
        {
            int baseMaxMana = MaxMana;

            // Add the "Moonlight" effect bonus if active
            int moonlightEffectValue = 0;
            if (ActiveEffects.Contains("moonlight"))
            {
                moonlightEffectValue = (int)(baseMaxMana * 0.1);
            }
            else
            {
                moonlightEffectValue = 0;
            }

            int newMaxMana = baseMaxMana + moonlightEffectValue;

            // Update the current mana to be appropriate to the new max mana
            if (CurrentMana > newMaxMana)
            {
                CurrentMana = newMaxMana;
            }

            return newMaxMana;
        }

    
        
        
        
        
        private string MapStatKey(string abbreviatedKey)
        {
            switch (abbreviatedKey.ToLower())
            {
                case "str":
                    return "Strength";
                case "dex":
                    return "Dexterity";
                case "int":
                    return "Intelligence";
                case "wis":
                    return "Wisdom";
                case "cha":
                    return "Charisma";
                case "con":
                    return "Constitution";
                default:
                    return string.Empty;
            }
        }
        public void RemoveFriend(string friendName)
        {
            if (!Friends.ContainsKey(friendName))
            {
                Connection.SendMessage($"{friendName} is not your friend.");
                return;
            }

            Friends.Remove(friendName);
            Connection.SendMessage($"You have removed {friendName} from your friend list.");
        }

        public Task Attack(Server server, Soul soul, string targetName)
        {
            if (targetName == null)
            {
                Connection.SendMessage("You must specify a target to attack.\r\n");
                return Task.CompletedTask;
            }

            if (targetName == soul.Name)
            {
                Connection.SendMessage("You cannot attack yourself.\r\n");
                return Task.CompletedTask;
            }

            Room currentRoom = server.GetRoomById(soul.CurrentRoomId);
            if (currentRoom == null)
            {
                Connection.SendMessage("You cannot attack anything right now.\r\n");
                return Task.CompletedTask;
            }

            if (soul.CurrentRoom == null || !soul.CurrentRoom.NPCsInRoom.Any())
            {
                Connection.SendMessage("There is nothing to attack here.\r\n");
                Console.WriteLine($"Debug: CurrentRoom is null: {soul.CurrentRoom == null}");
                Console.WriteLine($"Debug: NPCsInRoom count: {soul.CurrentRoom?.NPCsInRoom?.Count}");
                return Task.CompletedTask;
            }

            bool hasNPCs = soul.CurrentRoom.NPCsInRoom.Any();

            if (!hasNPCs)
            {
                Connection.SendMessage("There is nothing to attack here.\r\n");
                return Task.CompletedTask;
            }

            NPC target = null;
            foreach (NPC npc in soul.CurrentRoom.NPCsInRoom)
            {
                if (string.Equals(npc.Name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    target = npc;
                    break;
                }
            }

            if (target == null)
            {
                Connection.SendMessage("There is no one here by that name.\r\n");
                return Task.CompletedTask;
            }

            if (target.IsDead)
            {
                Connection.SendMessage("You cannot attack a dead target.\r\n");
                return Task.CompletedTask;
            }

            if (target.IsHidden)
            {
                Connection.SendMessage("You cannot attack a hidden target.\r\n");
                return Task.CompletedTask;
            }

            if (soul.IsAttacking)
            {
                Connection.SendMessage("You are already attacking.\r\n");
                return Task.CompletedTask;
            }


            soul.IsAttacking = true;
            target.IsAttacking = true;
            soul.CurrentTarget = target;
            target.CurrentTarget = soul;
            Connection.SendMessage($"You attack {target.Name}.\r\n");
// Send a message to everyone in the room except the soul and the target saying "<soul name> attacks <target name>"
            foreach (Soul soulInRoom in soul.CurrentRoom.PlayersInRoom.GroupBy(p => p.Name)
                         .Select(g => g.OrderByDescending(p => p.Connection.LastActive).First()))
            {
                if (soulInRoom.Name != soul.Name && soulInRoom.Name != target.Name)
                {
                    soulInRoom.Connection.SendMessage($"{soul.Name} attacks {target.Name}.\r\n");
                }
            }

// Call the AttackMessages method
            soul.AttackMessages(target, soul, null);

            return Task.CompletedTask;
        }
        public void ListFriends()
        {
            if (Friends.Count == 0)
            {
                Connection.SendMessage("You have no friends.");
                return;
            }

            var messageBuilder = new StringBuilder("Your friends:");
            foreach (var friendName in Friends)
            {
                messageBuilder.Append(" ");
                messageBuilder.Append(friendName);
            }

            Connection.SendMessage(messageBuilder.ToString());
        }

        public async void CastAbility(Server server, string abilityName, Soul soul, string targetName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(abilityName))
                {
                    Connection.SendMessage("Please specify an ability to cast.\n\r");
                    return;
                }

                // Find the target Soul by name
                var target =
                    server.Souls.FirstOrDefault(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"Casting ability: {abilityName}");
                Console.WriteLine($"Abilities: {string.Join(", ", Abilities)}");

                if (!Abilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase) &&
                    !soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                {
                    Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                    return;
                }

                // Add the logic for casting abilities here
                switch (abilityName.ToLower())
                {
                    case "spirit recall":
                        CurrentRoomId = soul.RecallRoom;
                        Connection.SendMessage($"\x1b[36mYou cast \x1b[0m" + $"\x1b[33m{abilityName}\x1b[0m\r\n");
                        break;
                    case "photosynthesis":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 10)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Check if it's daytime (between 6 am and 6 pm UTC)
                        DateTime nowUtc = DateTime.UtcNow;
                        bool isDayTimeUtc = (nowUtc.Hour >= 6) && (nowUtc.Hour < 18);

// Get the current room that the player is in
                        Room currentRoomPhotosynthesis = server.GetRoomById(soul.CurrentRoomId);

// Check if the room is not indoors
                        if (currentRoomPhotosynthesis.Indoor)
                        {
                            Connection.SendMessage("You can only cast photosynthesis outdoors.\r\n");
                            break;
                        }

                        if (!isDayTimeUtc)
                        {
                            Connection.SendMessage("You can only cast photosynthesis during the daytime (6 am to 6 pm UTC).\r\n");
                            break;
                        }


                        // Subtract 10 from current mana
                        soul.CurrentMana -= 10;

                        // Add "photosynthesis" active effect
                        if (!soul.ActiveEffects.Contains("photosynthesis"))
                        {
                            soul.ActiveEffects.Add("photosynthesis");
                        }

                        Connection.SendMessage("You have cast photosynthesis!\r\n");

                        // Create a timer to make the photosynthesis effect wear off after 15 minutes
                        System.Timers.Timer photosynthesisTimer = new System.Timers.Timer(15 * 60 * 1000);
                        photosynthesisTimer.AutoReset = false;
                        photosynthesisTimer.Elapsed += (sender, e) =>
                        {
                            soul.ActiveEffects.Remove("photosynthesis");
                            Connection.SendMessage("The photosynthesis spell has worn off.\r\n");
                            photosynthesisTimer.Dispose();
                        };
                        photosynthesisTimer.Start();

                        break;

                    case "blessing":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 5)
                        {
                            Connection.SendMessage(
                                $"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // If no target is specified, cast on self
                        if (string.IsNullOrEmpty(targetName))
                        {
                            if (soul.ActiveEffects.Contains("blessing"))
                            {
                                Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
                                break;
                            }

                            int oldMaxHealth = soul.MaxHealth;

                            // Add 5 to max health for 30 seconds
                            soul.MaxHealth += 5;
                            soul.ActiveEffects.Add("blessing");
                            Connection.SendMessage(
                                $"You feel blessed.\r\n\r\n");

                            // Subtract 5 from current mana
                            soul.CurrentMana -= 5;

                            // Generate a random number between 1 and 10
                            Random random = new Random();
                            int randomNumber = random.Next(1, 11);

                            // Check if the random number is 1, then increase the Faith skill by 1
                            if (randomNumber == 1)
                            {
                                soul.Skills["Faith"].CurrentValue += 1;
                                Connection.SendMessage("Your faith has increased by 1 point.\r\n");
                                server.SaveSoul(soul);
                            }

                            // Wait for 30 seconds and then remove the blessing
                            Task.Delay(30000).ContinueWith(t =>
                            {
                                soul.MaxHealth -= 5;

                                if (soul.CurrentHealth > soul.MaxHealth)
                                {
                                    soul.CurrentHealth = soul.MaxHealth;
                                    soul.ActiveEffects.Remove("blessing");
                                    Connection.SendMessage(
                                        $"The blessing has faded away.\r\n");
                                }
                                else
                                {
                                    soul.ActiveEffects.Remove("blessing");
                                    Connection.SendMessage(
                                        $"The blessing has faded away.\r\n");
                                }
                            });
                        }
                        else // Cast on target
                        {
                            if (target == null)
                            {
                                Connection.SendMessage($"Target '{targetName}' not found.\r\n");
                                break;
                            }

                            // Check if the target already has the "blessing" effect
                            if (target.ActiveEffects.Contains("blessing"))
                            {
                                Connection.SendMessage(
                                    $"{target.Name} already has \x1b[33m{abilityName}\x1b[0m active.\r\n");
                                break;
                            }

                            // Subtract 5 from current mana
                            soul.CurrentMana -= 5;

                            int oldMaxHealth = target.MaxHealth;

                            // Add 5 to max health for 30 seconds
                            target.MaxHealth += 5;
                            target.ActiveEffects.Add("blessing");
                            Connection.SendMessage(
                                $"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}.\r\n\r\n");

                            // Wait for 30 seconds and then remove the blessing
                            Task.Delay(30000).ContinueWith(t =>
                            {
                                target.MaxHealth -= 5;

                                if (target.CurrentHealth > target.MaxHealth)
                                {
                                    target.CurrentHealth = target.MaxHealth;
                                    target.ActiveEffects.Remove("blessing");
                                    Connection.SendMessage(
                                        $"{target.Name}'s blessing has faded away.\r\n");
                                }
                                else
                                {
                                    target.ActiveEffects.Remove("blessing");
                                    Connection.SendMessage(
                                        $"{target.Name}'s blessing has faded away.\r\n");
                                }
                            });
                        }

                        break;
                    case "minor illusion":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 10)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Subtract 10 from current mana
                        soul.CurrentMana -= 10;

                        // Generate a random number between 1 and the number of objects in the list
                        Random illusionRandom = new Random();
                        int illusionRandomNumber = illusionRandom.Next(1, objectList.Count);

                        // Set the player's IsIllusion flag to true and remember the name of the object they turned into
                        soul.IsIllusion = true;
                        soul.IllusionObject = objectList[illusionRandomNumber - 1];

                        Connection.SendMessage($"You have become a {soul.IllusionObject}!\r\n");
                        // Create a timer to make the illusion wear off after 15 minutes
                        System.Timers.Timer illusionTimer = new System.Timers.Timer(15 * 60 * 1000);
                        illusionTimer.AutoReset = false;
                        illusionTimer.Elapsed += (sender, e) =>
                        {
                            soul.IsIllusion = false;
                            soul.IllusionObject = null;
                            Connection.SendMessage("The minor illusion spell has worn off, and you are no longer disguised.\r\n");
                            illusionTimer.Dispose();
                        };
                        illusionTimer.Start();

                        break;
                    case "plant form":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 10)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Subtract 10 from current mana
                        soul.CurrentMana -= 10;

                        // Generate a random number between 1 and the number of objects in the list
                        Random plantRandom = new Random();
                        int plantRandomNumber = plantRandom.Next(1, plantList.Count);

                        // Set the player's IsIllusion flag to true and remember the name of the object they turned into
                        soul.IsIllusion = true;
                        soul.IllusionObject = plantList[plantRandomNumber - 1];

                        Connection.SendMessage($"\x1b[38;2;124;252;0mYou have become a {soul.IllusionObject}!\x1b[0m\r\n");
                        // Add the flavor message to be displayed to everyone in the room
                        // Get the current room that the player is in
                        Room currentRoomPlantForm = server.GetRoomById(soul.CurrentRoomId);
                        await currentRoomPlantForm.SendMessageToAllPlayers(
                            $"\x1b[38;2;124;252;0m{soul.Name} turns into a {soul.IllusionObject}.\x1b[0m\r\n", soul);
                        // Create a timer to make the illusion wear off after 15 minutes
                        System.Timers.Timer plantTimer = new System.Timers.Timer(15 * 60 * 1000);
                        plantTimer.AutoReset = false;
                        plantTimer.Elapsed += (sender, e) =>
                        {
                            soul.IsIllusion = false;
                            soul.IllusionObject = null;
                            Connection.SendMessage("\x1b[38;2;124;252;0mThe plant form spell has worn off, and you are no longer disguised.\x1b[0m\r\n");
                            plantTimer.Dispose();
                        };
                        plantTimer.Start();

                        break;
                    case "minor familiar":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 20)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Subtract 20 from current mana
                        soul.CurrentMana -= 20;

                        // Create a new NPC as the familiar
                        List<int> familiarAllowedRooms = new List<int> { soul.CurrentRoomId };
                        NPC familiar = new NPC(server, 0, $"{soul.Name}'s familiar", 1, soul.CurrentRoomId, 10, 10, 10, 10, "A loyal familiar summoned by its master.", familiarAllowedRooms, 1, 1, null, "", false, 10);

                        // Add the familiar to the current room
                        Room familiarRoom = _server.GetRoomById(soul.CurrentRoomId);
                        familiarRoom.AddNPC(familiar, _server);

                        // Send a message to the player and the room
                        Connection.SendMessage("You have summoned a minor familiar!\r\n");
                        familiarRoom.SendMessageToAllPlayers($"{soul.Name} has summoned a minor familiar!", soul);

                        // Attach an event handler to remove the familiar when the player leaves the room
                        soul.PlayerLeavingRoom += (sender, e) =>
                        {
                            familiarRoom.RemoveNPC(familiar, _server);
                            familiarRoom.SendMessageToAllPlayers($"{familiar.Name} scampered away as {soul.Name} left the room.", soul);
                        };

                        break;
                    case "whispering wind":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 5)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Set the IsWind flag on the caster
                        soul.IsWind = true;

                        // Subtract mana cost from current mana
                        soul.CurrentMana -= 4;

                        // Send a message to the player
                        Connection.SendMessage("\x1b[38;2;173;216;230mYou are cloaked in an illusory wind.\x1b[0m\r\n");
                        soul.ActiveEffects.Add("whispering wind");
                        // Add the flavor message to be displayed to everyone in the room
                        // Get the current room that the player is in
                        Room currentRoomWhisperingWind = server.GetRoomById(soul.CurrentRoomId);
                        await currentRoomWhisperingWind.SendMessageToAllPlayers(
                            $"\x1b[38;2;173;216;230mAn illusory wind swirls around {soul.Name}.\x1b[0m\r\n", soul);

                        // Schedule the removal of the IsWind flag after 5 minutes
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromMinutes(5));
                            soul.IsWind = false;
                            Connection.SendMessage("\x1b[38;2;173;216;230mThe whispering wind fades.\x1b[0m\r\n");
                            soul.ActiveEffects.Remove("whispering wind");

                        });

                        break;

                          case "hot radiation":
                            // Check if the player has the ability in their LearnedAbilities list
                            if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                            {
                                Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                                break;
                            }

                            // Check if the player has enough mana to cast the spell
                            if (soul.CurrentMana < 10)
                            {
                                Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                                break;
                            }

                            // If no target is specified, cast on self
                            if (string.IsNullOrEmpty(targetName))
                            {
                                if (soul.ActiveEffects.Contains("hot radiation"))
                                {
                                    Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
                                    break;
                                }

                                // Apply the effect
                                soul.ActiveEffects.Add("hot radiation");
                                Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on yourself.\r\n");
                                // Add the flavor message to be displayed to everyone in the room
                                // Get the current room that the player is in
                                Room currentRoomHotRadiation = server.GetRoomById(soul.CurrentRoomId);
                                await currentRoomHotRadiation.SendMessageToAllPlayers(
                                    $"\x1b[38;2;255;69;0m{soul.Name} radiates heat.\x1b[0m\r\n", soul);

                                // Subtract 10 from current mana
                                soul.CurrentMana -= 10;

                                // Wait for 5 minutes and then remove the hot radiation effect
                                Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t =>
                                {
                                    soul.ActiveEffects.Remove("hot radiation");
                                    Connection.SendMessage($"You cool down.\r\n");
                                });
                            }
                            else // Cast on target
                            {
                                if (target == null)
                                {
                                    Connection.SendMessage($"Target '{targetName}' not found.\r\n");
                                    break;
                                }

                                // Check if the target already has the "hot radiation" effect
                                if (target.ActiveEffects.Contains("hot radiation"))
                                {
                                    Connection.SendMessage($"{target.Name} already has \x1b[33m{abilityName}\x1b[0m active.\r\n");
                                    break;
                                }

                                // Apply the effect
                                target.ActiveEffects.Add("hot radiation");
                                Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}.\r\n");

                                // Subtract 10 from current mana
                                soul.CurrentMana -= 10;

                                // Wait for 5 minutes and then remove the hot radiation effect
                                Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t =>
                                {
                                    target.ActiveEffects.Remove("hot radiation");
                                    Connection.SendMessage($"{target.Name}'s hot radiation effect has faded away.\r\n");
                                });
                            }

                            break;

                        case "cold fusion shield":
                            // Check if the player has the ability in their LearnedAbilities list
                            if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                            {
                                Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                                break;
                            }

    // If no target is specified, cast on self
    if (string.IsNullOrEmpty(targetName))
    {
        if (soul.ActiveEffects.Contains("cold fusion shield"))
        {
            Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        // Apply the effect
        soul.ActiveEffects.Add("cold fusion shield");
        Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on yourself.\r\n");

        // Wait for 10 minutes and then remove the cold fusion shield effect
        Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(t =>
        {
            soul.ActiveEffects.Remove("cold fusion shield");
            Connection.SendMessage($"The cold fusion shield effect has faded away.\r\n");
        });
    }
    else // Cast on target
    {
        if (target == null)
        {
            Connection.SendMessage($"Target '{targetName}' not found.\r\n");
            break;
        }

        // Check if the target already has the "cold fusion shield" effect
        if (target.ActiveEffects.Contains("cold fusion shield"))
        {
            Connection.SendMessage($"{target.Name} already has \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        // Apply the effect
        target.ActiveEffects.Add("cold fusion shield");
        Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}.\r\n");

        // Wait for 10 minutes and then remove the cold fusion shield effect
        Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(t =>
        {
            target.ActiveEffects.Remove("cold fusion shield");
            Connection.SendMessage($"{target.Name}'s cold fusion shield effect has faded away.\r\n");
        });
    }

    break;

                    case "mindbright":
    // Check if the player has the ability in their LearnedAbilities list
    if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
    {
        Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
        break;
    }

    // Check if the player has enough mana to cast the spell
    if (soul.CurrentMana < 5)
    {
        Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
        break;
    }

    // If no target is specified, cast on self
    if (string.IsNullOrEmpty(targetName))
    {
        if (soul.ActiveEffects.Contains("mindbright"))
        {
            Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        // Add 5 to max mana for 30 seconds
        soul.MaxMana += 5;
        soul.ActiveEffects.Add("mindbright");
        Connection.SendMessage(
            $"You cast \x1b[33m{abilityName}\x1b[0m on yourself.\r\n\r\n");

        // Subtract 5 from current mana
        soul.CurrentMana -= 5;

        // Wait for 30 seconds and then remove the mindbright effect
        Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t =>
        {
            soul.MaxMana -= 5;
            soul.ActiveEffects.Remove("mindbright");
            Connection.SendMessage($"The mindbright effect has faded away.\r\n");
        });
    }
    else // Cast on target
    {
        if (target == null)
        {
            Connection.SendMessage($"Target '{targetName}' not found.\r\n");
            break;
        }

        // Check if the target already has the "mindbright" effect
        if (target.ActiveEffects.Contains("mindbright"))
        {
            Connection.SendMessage($"{target.Name} already has \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        int oldMaxMana = target.MaxMana;

        // Add 5 to max mana for 30 seconds
        target.MaxMana += 5;
        target.ActiveEffects.Add("mindbright");
        Connection.SendMessage(
            $"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}.\r\n\r\n");

        // Subtract 5 from current mana
        soul.CurrentMana -= 5;

        // Wait for 30 seconds and then remove the mindbright
        Task.Delay(30000).ContinueWith(t =>
        {
            target.MaxMana -= 5;

            if (target.CurrentMana > target.MaxMana)
            {
                target.CurrentMana = target.MaxMana;
                target.ActiveEffects.Remove("mindbright");
                Connection.SendMessage(
                    $"{target.Name}'s mindbright has faded away.\r\n");
            }
            else
            {
                target.ActiveEffects.Remove("mindbright");
                Connection.SendMessage(
                    $"{target.Name}'s mindbright has faded away.\r\n");
            }
        });
    }

    break;

                    case "dark pact":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough health to cast the spell
                        if (soul.CurrentHealth < 5)
                        {
                            Connection.SendMessage(
                                $"You do not have enough health to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // If no target is specified, cast on self
                        if (string.IsNullOrEmpty(targetName))
                        {
                            if (soul.ActiveEffects.Contains("dark pact"))
                            {
                                Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
                                break;
                            }

                            // Decrease 1 from current health regeneration and add 2 to current mana regeneration
                            soul.HealthRegenModifier -= 1;
                            soul.ManaRegenModifier += 2;
                            soul.ActiveEffects.Add("dark pact");
                            Connection.SendMessage(
                                $"\x1b[38;2;102;51;153mYou enter into a \x1b[33m{abilityName}\x1b[0m...\x1b[0m\r\n\r\n");
                            // Add the flavor message to be displayed to everyone in the room
                            // Get the current room that the player is in
                            Room currentRoomDarkPact = server.GetRoomById(soul.CurrentRoomId);
                            await currentRoomDarkPact.SendMessageToAllPlayers(
                                $"\x1b[38;2;102;51;153m{soul.Name} mutters strange words and their eyes go dark.\x1b[0m\r\n", soul);
                            // Subtract 5 from current health
                            soul.CurrentHealth -= 5;

                            // Generate a random number between 1 and 10
                            Random random = new Random();
                            int randomNumber = random.Next(1, 11);

                            // Check if the random number is 1, then increase the Occultism skill by 1
                            if (randomNumber == 1)
                            {
                                soul.Skills["Occultism"].CurrentValue += 1;
                                Connection.SendMessage("Your occultism has increased by 1 point.\r\n");
                                server.SaveSoul(soul);
                            }
                        }
                        break;

case "cosmic essence":
    // Check if the player has the ability in their LearnedAbilities list
    if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
    {
        Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
        break;
    }

    // Check if the player has enough mana to cast the spell
    if (soul.CurrentMana < 5)
    {
        Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
        break;
    }

    // Subtract 5 from current mana
    soul.CurrentMana -= 5;

    // If no target is specified, cast on self
    if (string.IsNullOrEmpty(targetName))
    {
        if (soul.CosmicEssenceActive)
        {
            Connection.SendMessage($"You already have \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        soul.CosmicEssenceActive = true;
        soul.ActiveEffects.Add("cosmic essence");
        Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on yourself, increasing your mana regeneration.\r\n");

        // Wait for 5 minutes and then remove the cosmic essence effect
        Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t =>
        {
            soul.CosmicEssenceActive = false;
            soul.ActiveEffects.Remove("cosmic essence");
            Connection.SendMessage($"Your \x1b[33m{abilityName}\x1b[0m effect has faded away.\r\n");
        });
    }
    else // Cast on target
    {
        if (target == null)
        {
            Connection.SendMessage($"Target '{targetName}' not found.\r\n");
            break;
        }

        if (target.CosmicEssenceActive)
        {
            Connection.SendMessage($"{target.Name} already has \x1b[33m{abilityName}\x1b[0m active.\r\n");
            break;
        }

        target.CosmicEssenceActive = true;
        target.ActiveEffects.Add("cosmic essence");
        Connection.SendMessage(
            $"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}, increasing their mana regeneration.\r\n");

        // Wait for 5 minutes and then remove the cosmic essence effect
        Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t =>
        {
            target.CosmicEssenceActive = false;
            target.ActiveEffects.Remove("cosmic essence");
            Connection.SendMessage(
                $"{target.Name}'s \x1b[33m{abilityName}\x1b[0m effect has faded away.\r\n");
        });
    }

    break;


                    case "salve":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }
                        // Check if the spell is on cooldown
                        TimeSpan salveCooldown = TimeSpan.FromSeconds(10);
                        if (DateTime.UtcNow - soul.SalveLastCastTime < salveCooldown)
                        {
                            TimeSpan timeRemaining = salveCooldown - (DateTime.UtcNow - soul.SalveLastCastTime);
                            Connection.SendMessage($"You must wait {timeRemaining.Seconds} seconds before casting \x1b[33m{abilityName}\x1b[0m again.\r\n");
                            break;
                        }

                        // Update the last cast time for the "salve" ability
                        soul.SalveLastCastTime = DateTime.UtcNow;
                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 5)
                        {
                            Connection.SendMessage(
                                $"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // If a target is specified, find the target
                        if (!string.IsNullOrEmpty(targetName))
                        {
                            target = server.GetSoulByName(targetName);
                            if (target == null)
                            {
                                Connection.SendMessage($"Cannot find target named \x1b[33m{targetName}\x1b[0m.\r\n");
                                break;
                            }
                        }

                        // Check if the target's health is already full
                        if (target.CurrentHealth == target.MaxHealth)
                        {
                            Connection.SendMessage(
                                $"Cannot cast \x1b[33m{abilityName}\x1b[0m on {target.Name}. Their health is already full.\r\n");
                            break;
                        }

                        // Subtract 5 from current mana
                        soul.CurrentMana -= 5;
                        // Calculate the new health after healing
                        int newHealth = target.CurrentHealth + 5;

                        // Ensure the new health doesn't exceed the maximum health value
                        target.CurrentHealth = Math.Min(newHealth, target.MaxHealth);
                        // Generate random numbers for skill increases
                        Random randomGenerator = new Random();
                        int randomNumberWhiteMagic = randomGenerator.Next(1, 21);
                        int randomNumberWoundTending = randomGenerator.Next(1, 11);
                        int randomNumberFaith = randomGenerator.Next(1, 31);

                        // Increase the White Magic skill by 1 if the random number is 1
                        if (randomNumberWhiteMagic == 1)
                        {
                            soul.Skills["White Magic"].CurrentValue += 1;
                            Connection.SendMessage("Your white magic has increased by 1 point.\r\n");
                            server.SaveSoul(soul);
                        }

                        // Increase the Wound Tending skill by 1 if the random number is 1
                        if (randomNumberWoundTending == 1)
                        {
                            soul.Skills["Wound Tending"].CurrentValue += 1;
                            Connection.SendMessage("Your wound tending has increased by 1 point.\r\n");
                            server.SaveSoul(soul);
                        }

                        // Increase the Faith skill by 1 if the random number is 1
                        if (randomNumberFaith == 1)
                        {
                            soul.Skills["Faith"].CurrentValue += 1;
                            Connection.SendMessage("Your faith has increased by 1 point.\r\n");
                            server.SaveSoul(soul);
                        }

                        if (target == soul)
                        {
                            Connection.SendMessage(
                                $"You cast \x1b[33m{abilityName}\x1b[0m on yourself. Your health is restored by 5 points.\r\n");
                        }
                        else
                        {
                            Connection.SendMessage(
                                $"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}. Their health is restored by 5 points.\r\n");
                        }

                        break;

                    case "coldfire":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }
                        // Check if the spell is on cooldown
                        TimeSpan coldfireCooldown = TimeSpan.FromSeconds(10);
                        if (DateTime.UtcNow - soul.ColdfireLastCastTime < coldfireCooldown)
                        {
                            TimeSpan timeRemaining = coldfireCooldown - (DateTime.UtcNow - soul.ColdfireLastCastTime);
                            Connection.SendMessage($"You must wait {timeRemaining.Seconds} seconds before casting \x1b[33m{abilityName}\x1b[0m again.\r\n");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 5)
                        {
                            Connection.SendMessage(
                                $"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Subtract 5 from current mana
                        soul.CurrentMana -= 5;

                        // Cast on target
                        if (string.IsNullOrEmpty(targetName))
                        {
                            Connection.SendMessage("Please provide a target name for the spell.\r\n");
                            break;
                        }

                        // Get the current room that the player is in
                        Room currentRoomColdfire = server.GetRoomById(soul.CurrentRoomId);

                        // Find the target NPC in the current room
                        NPC targetNPCColdfire = null;
                        foreach (NPC npc in currentRoomColdfire.NPCsInRoom)
                        {
                            if (npc.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetNPCColdfire = npc;
                                break;
                            }
                        }

                        // Apply the effect to the target NPC if found
                        if (targetNPCColdfire != null)
                        {
                            int newHealthColdfire = targetNPCColdfire.CurrentHealth - 5;
                            targetNPCColdfire.CurrentHealth = Math.Max(newHealthColdfire, 0);
                            Connection.SendMessage($"You cast \x1b[33m{abilityName}\x1b[0m on {targetNPCColdfire.Name}. Their health is reduced by 5 points.\r\n");
                        }
                        else
                        {
                            Connection.SendMessage("The target could not be found.\r\n");
                        }
                        // Generate random numbers for skill increases
                        Random randomGeneratorColdfire = new Random();
                        int randomNumberBlueMagic = randomGeneratorColdfire.Next(1, 21);
                        int randomNumberArcanism = randomGeneratorColdfire.Next(1, 11);


                        // Increase the Blue Magic skill by 1 if the random number is 1
                        if (randomNumberBlueMagic == 1)
                        {
                            soul.Skills["Blue Magic"].CurrentValue += 1;
                            Connection.SendMessage("Your blue magic has increased by 1 point.\r\n");
                            server.SaveSoul(soul);
                        }

                        // Increase the Arcanism skill by 1 if the random number is 1
                        if (randomNumberArcanism == 1)
                        {
                            soul.Skills["Arcanism"].CurrentValue += 1;
                            Connection.SendMessage("Your arcanism has increased by 1 point.\r\n");
                            server.SaveSoul(soul);
                        }
                        // Set the last cast time for the "coldfire" spell
                        soul.ColdfireLastCastTime = DateTime.UtcNow;
                        if (target == soul)
                        {
                            Connection.SendMessage(
                                $"You cast \x1b[33m{abilityName}\x1b[0m on yourself.\r\n");
                        }
                        else
                        {
                            Connection.SendMessage(
                                $"You cast \x1b[33m{abilityName}\x1b[0m on {target.Name}.\r\n");
                        }

                        break;
                    default:
                        Connection.SendMessage($"You cast the '{abilityName}' ability!");
                        break;
                    
                    case "inspire":
                        // Check if a target is specified
                        if (string.IsNullOrEmpty(targetName))
                        {
                            Connection.SendMessage("You need to specify a target player to inspire.\r\n");
                            break;
                        }

                        // Check if the target is not the player themselves
                        if (targetName.Equals(soul.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage("You cannot inspire yourself.\r\n");
                            break;
                        }

                        // Find the target player in the current room
                        Soul targetSoul = server.GetRoomById(soul.CurrentRoomId).PlayersInRoom
                            .FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                        if (targetSoul == null)
                        {
                            Connection.SendMessage($"Could not find the player '{targetName}' in this room.\r\n");
                            break;
                        }

                        // Apply the "inspire" effect to the target player
                        if (!targetSoul.ActiveEffects.Contains("inspire"))
                        {
                            targetSoul.ActiveEffects.Add("inspire");
                            Connection.SendMessage(
                                $"You inspire {targetSoul.Name}, increasing their strength and dexterity.\r\n");
                            targetSoul.Connection.SendMessage(
                                $"{soul.Name} inspires you, increasing your strength and dexterity.\r\n");

                            // Remove the effect and reset the stats after 10 minutes
                            Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(t =>
                            {
                                targetSoul.ActiveEffects.Remove("inspire");
                                targetSoul.Connection.SendMessage(
                                    "The feel less inspired.\r\n");
                            });
                        }
                        else
                        {
                            Connection.SendMessage($"{targetSoul.Name} is already inspired.\r\n");
                        }

                        break;
                    
                    case "blood pact":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough health to cast the spell
                        if (soul.CurrentHealth <= 1)
                        {
                            Connection.SendMessage(
                                $"You do not have enough health to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // If a target is specified, find the target
                        if (!string.IsNullOrEmpty(targetName))
                        {
                            target = server.GetSoulByName(targetName);
                            if (target == null)
                            {
                                Connection.SendMessage($"Cannot find target named \x1b[33m{targetName}\x1b[0m.\r\n");
                                break;
                            }
                        }

                        // Check if the target's health is already full
                        if (target.CurrentHealth == target.MaxHealth)
                        {
                            Connection.SendMessage(
                                $"Cannot cast \x1b[33m{abilityName}\x1b[0m on {target.Name}. Their health is already full.\r\n");
                            break;
                        }

                        int transferCount = 0;
                        CancellationTokenSource cts = new CancellationTokenSource();

                        async Task TransferHealth()
                        {
                            while (transferCount < 15 && !cts.Token.IsCancellationRequested)
                            {
                                await Task.Delay(9000);
                                if (soul.CurrentHealth <= 1)
                                {
                                    Connection.SendMessage($"You don't have enough health to continue the \x1b[33m{abilityName}\x1b[0m.\r\n");
                                    cts.Cancel();
                                    break;
                                }

                                soul.CurrentHealth -= 1;
                                target.CurrentHealth = Math.Min(target.CurrentHealth + 1, target.MaxHealth);
                                transferCount++;
                            }

                            if (!cts.Token.IsCancellationRequested)
                            {
                                Connection.SendMessage($"The \x1b[33m{abilityName}\x1b[0m effect has worn off.\r\n");
                            }
                        }

                        Connection.SendMessage($"You enter into a \x1b[33m{abilityName}\x1b[0m with {target.Name}.\r\n");
                        // Add the flavor message to be displayed to everyone in the room
                        // Get the current room that the player is in
                        Room currentRoomBloodPact = server.GetRoomById(soul.CurrentRoomId);
                        await currentRoomBloodPact.SendMessageToAllPlayers(
                            $"{soul.Name} enters into a blood pact with {target.Name}.\r\n", soul);
                        target.Connection.SendMessage(
                            $"{soul.Name} initiates a blood pact with you.\r\n");

                        
                        _ = TransferHealth();
                        break;
                    case "petrify":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Check if the player has enough mana to cast the spell
                        if (soul.CurrentMana < 10)
                        {
                            Connection.SendMessage($"You do not have enough mana to cast \x1b[33m{abilityName}\x1b[0m.\r\n");
                            break;
                        }

                        // Subtract 10 from current mana
                        soul.CurrentMana -= 10;

                        // Cast on target
                        if (string.IsNullOrEmpty(targetName))
                        {
                            Connection.SendMessage("Please provide a target name for the spell.\r\n");
                            break;
                        }

                        // Get the current room that the player is in
                        Room currentRoom = server.GetRoomById(soul.CurrentRoomId);

                        // Find the target NPC in the current room
                        NPC targetNPC = null;
                        foreach (NPC npc in currentRoom.NPCsInRoom)
                        {
                            if (npc.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetNPC = npc;
                                break;
                            }
                        }

                        // Apply the effect to the target NPC if found
                        if (targetNPC != null)
                        {
                            targetNPC.IsPetrified = true;
                            Connection.SendMessage($"You petrify {targetNPC.Name} with your gaze!\r\n");
                            // Add the flavor message to be displayed to everyone in the room
                            // Get the current room that the player is in
                            Room currentRoomPetrify = server.GetRoomById(soul.CurrentRoomId);
                            currentRoomPetrify.SendMessageToAllPlayers(
                                $"{soul.Name} petrifies {target.Name} with their gaze.\r\n", soul);

                            // Wait for 5 seconds and then remove the petrifying gaze effect
                            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t =>
                            {
                                targetNPC.IsPetrified = false;
                                Connection.SendMessage($"The \x1b[33m{abilityName}\x1b[0m effect on {targetNPC.Name} has faded away.\r\n");
                                currentRoomPetrify.SendMessageToAllPlayers(
                                    $"{target.Name} recovers from petrification.\r\n", soul);
                            });
                        }
                        else
                        {
                            Connection.SendMessage("The target could not be found.\r\n");
                        }

                        break;

                    case "shadowstep":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Get the current room
                        Room currentRoomShadowstep = server.GetRoomById(soul.CurrentRoomId);

                        // Get a list of connected rooms
                        List<Room> connectedRooms = currentRoomShadowstep.Exits.Values.ToList();

                        // Select a random connected room
                        Random rng = new Random();
                        int randomIndex = rng.Next(0, connectedRooms.Count);
                        Room randomConnectedRoom = connectedRooms[randomIndex];

                        // Teleport the player to the random connected room
                        currentRoomShadowstep.RemovePlayerFromRoom(soul);
                        soul.CurrentRoomId = randomConnectedRoom.ID;
                        randomConnectedRoom.AddPlayerToRoom(soul, _server, randomConnectedRoom);

                        // Apply the "Shadowstep" effect to the player
                        soul.ActiveEffects.Add("Shadowstep");
                        Connection.SendMessage($"You \x1b[33m{abilityName}\x1b[0m away to the next room.\r\n");
                        


                        // Wait for 40 seconds and then remove the "Shadowstep" effect
                        Task.Delay(TimeSpan.FromSeconds(40)).ContinueWith(t =>
                        {
                            soul.ActiveEffects.Remove("Shadowstep");
                            Connection.SendMessage($"The \x1b[33m{abilityName}\x1b[0m effect has faded away.\r\n");
                            currentRoomShadowstep.SendMessageToAllPlayers(
                                $"{soul.Name} shadowsteps away.\r\n", soul);
                        });

                        break;
                    case "blinkstep":
                        // Check if the player has the ability in their LearnedAbilities list
                        if (!soul.LearnedAbilities.Contains(abilityName, StringComparer.OrdinalIgnoreCase))
                        {
                            Connection.SendMessage($"You do not have access to the '{abilityName}' ability.\n\r");
                            break;
                        }

                        // Get the current room
                        Room currentRoomBlinkstep = server.GetRoomById(soul.CurrentRoomId);

                        // Get a list of connected rooms
                        List<Room> connectedRoomsBlinkstep = currentRoomBlinkstep.Exits.Values.ToList();

                        // Select a random connected room
                        Random rngBlinkstep = new Random();
                        int randomIndexBlinkstep = rngBlinkstep.Next(0, connectedRoomsBlinkstep.Count);
                        Room randomConnectedRoomBlinkstep = connectedRoomsBlinkstep[randomIndexBlinkstep];

                        // Teleport the player to the random connected room
                        currentRoomBlinkstep.RemovePlayerFromRoom(soul);
                        soul.CurrentRoomId = randomConnectedRoomBlinkstep.ID;
                        randomConnectedRoomBlinkstep.AddPlayerToRoom(soul, _server, randomConnectedRoomBlinkstep);
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CastAbility: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private string GetOppositeDirection(string direction)
        {
            switch (direction.ToLower())
            {
                case "north":
                    return "south";
                case "south":
                    return "north";
                case "east":
                    return "west";
                case "west":
                    return "east";
                default:
                    return "unknown";
            }
        }

        public void UpdateThreat(NPC npc, int threat)
        {
            Console.WriteLine($"Updating threat for {Name} against {npc.Name} by {threat}");
            npc.UpdateThreat(this, threat);
        }


        public void AddCurrency(string currency, int amount)
        {
            switch (currency.ToLower())
            {
                case "astrals":
                    Astrals += amount;
                    break;
                case "voidastrals":
                    VoidAstrals += amount;
                    break;
                case "impcoins":
                    Impcoins += amount;
                    break;
                default:
                    throw new ArgumentException($"Invalid currency name: {currency}");
            }
        }

        public abstract class PlayerClass
        {
            public virtual string Name => string.Empty;

            public virtual Dictionary<string, int> Attributes => new Dictionary<string, int>();

            public virtual Dictionary<string, int> Skills => new Dictionary<string, int>();

            public virtual List<string> Abilities => new List<string>();

            public abstract bool CanBecomeClass(Soul soul);

            public abstract void ApplyClass(Soul soul);

            public static PlayerClass GetPlayerClassByName(string className)
            {
                switch (className.ToLower())
                {
                    case "traveler":
                        return new Traveler();
                    case "etherealist":
                        return new Etherealist();
                    case "priest":
                        return new Priest();
                    case "rogue":
                        return new Rogue();
                    case "sage":
                        return new Sage();
                    case "wizard":
                        return new Wizard();
                    case "occultist":
                        return new Occultist();
                    case "brawler":
                        return new Brawler();
                    case "swordsman":
                        return new Swordsman();
                    case "psychic":
                        return new Psychic();
                    case "vampire":
                        return new Vampire();
                    case "illusionist":
                        return new Illusionist();
                    case "hedgecrafter":
                        return new Hedgecrafter();
                    // Add other player class cases here...
                    default:
                        throw new ArgumentException($"Unknown player class '{className}'.");
                }
            }
        }
        
        public static string GetColoredClassName(string className)
        {
            switch (className.ToLower())
            {
                case "traveler":
                    return "\x1b[92m" + className + "\x1b[0m"; // Green
                case "etherealist":
                    return "\x1b[94m" + className + "\x1b[0m"; // Blue
                case "priest":
                    return "\x1b[93m" + className + "\x1b[0m"; // Yellow
                case "rogue":
                    return "\x1b[91m" + className + "\x1b[0m"; // Red
                case "sage":
                    return "\x1b[96m" + className + "\x1b[0m"; // Cyan
                case "occultist":
                    return "\x1b[38;2;153;50;204m" + className + "\x1b[0m"; // Dark Orchid 
                case "wizard":
                    return "\x1b[96m" + className + "\x1b[0m"; // Cyan
                case "brawler":
                    return "\x1b[93m" + className + "\x1b[0m"; // Yellow
                case "swordsman":
                    return "\x1b[92m" + className + "\x1b[0m"; // Green
                case "psychic":
                    return "\x1b[38;2;120;81;169m" + className + "\x1b[0m"; // Royal Purple
                case "vampire":
                    return "\x1b[91m" + className + "\x1b[0m"; // Red
                case "illusionist":
                    return "\x1b[38;2;255;45;100m" + className + "\x1b[0m"; // Bright Scarlet
                case "hedgecrafter":
                    return "\x1b[38;2;124;252;0m" + className + "\x1b[0m"; // Lawn Green

                default:
                    throw new ArgumentException($"Unknown player class '{className}'.");
            }
        }

        private string ComputeHash(string password, string salt)
        {
            using (var sha = SHA256.Create())
            {
                var saltedPassword = password + salt;
                var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        private CancellationTokenSource _attackMessageTokenSource;

    public void AttackMessages(NPC targetNPC, Soul soul, Group group)
{
    // Check if the player is attacking
    if (IsAttacking)
    {
        // Cancel any previous AttackMessages task
        _attackMessageTokenSource?.Cancel();
        // Calculate the delay time based on the soul's total dexterity value and level
        int totalDexterityValue = GetTotalAttributeValue("Dexterity");
        int totalStrengthValue = GetTotalAttributeValue("Strength");
        int level = Level;
        double delayTime = BASE_ATTACK_DELAY_TIME / (level + (totalDexterityValue * 0.25));
        // Start a new task to send messages with the calculated delay time
        _attackMessageTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _attackMessageTokenSource.Token;
        Task.Run(async () =>
        {
            bool tutorialMessageSent = false;

            while (IsAttacking)
            {
                // Check if the task has been cancelled
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                // Send the tutorial message if the player is in room 27 and the message hasn't been sent yet
                if (CurrentRoomId == 27 && !tutorialMessageSent)
                {
                    Connection.SendMessage("\r\n\x1b[32mHINT: You've initiated autoattack with an enemy! Your attacks will start out slow, but as you gain power and equipment, they'll increase in speed. For now, just watch your autoattack take down your target.\x1b[0m\r\n");
                    tutorialMessageSent = true;
                }
                string skillToIncrease = null;
                    double baseChance = 0.2;
                    // Check if there's nothing equipped in the "Primary" equipment slot
                    if (Equipment[16] == null)
                    {
                        skillToIncrease = "Hand to Hand";
                    }
                    // Check if the "Type" of the item in equipment slot 16 is "Slashing"
                    else if (Equipment[16].Type == "Slashing")
                    {
                        skillToIncrease = "Slashing";
                    }
                    // Check if the "Type" of the item in equipment slot 16 is "Blunt"
                    else if (Equipment[16].Type == "Blunt")
                    {
                        skillToIncrease = "Blunt";
                    }
                    // Check if the "Type" of the item in equipment slot 16 is "Piercing"
                    else if (Equipment[16].Type == "Piercing")
                    {
                        skillToIncrease = "Piercing";
                    }
                    // If a skill to increase is identified, increase the skill
                    if (skillToIncrease != null)
                    {
                        int currentSkillValue = Skills[skillToIncrease].CurrentValue;
                        IncreaseSkill(skillToIncrease, currentSkillValue, baseChance, Level);
                    }
                    // Calculate the damage based on the player's level and total strength value
                    int damage = level + (totalStrengthValue * 2);

// Add chance to miss
                Room currentRoom = _server.GetRoomById(CurrentRoomId);

                if (new Random().Next(0, 10) == 0)
                {
                    damage = 0;
                    Connection.SendMessage("Your attack misses the target!\r\n");
                    await currentRoom.SendMessageToAllPlayers($"{Name}'s attack misses {CurrentTarget.Name}!\r\n", this);
                }
                else
                {
                    if (new Random().Next(0, 30) == 0)
                    {
                        damage *= 2;
                        Connection.SendMessage("You land a critical hit!\r\n");
                        await currentRoom.SendMessageToAllPlayers($"{Name} lands a critical hit on {CurrentTarget.Name}!\r\n", this);
                    }
                    else
                    {
                        int variation = new Random().Next(-2, 3);
                        damage += variation;

                        if (variation > 0)
                        {
                            Connection.SendMessage($"You deal {damage} damage, a critical hit!\r\n");
                            await currentRoom.SendMessageToAllPlayers($"{Name} deals {damage} damage to {CurrentTarget.Name}, a critical hit!\r\n", this);
                        }
                        else if (variation < 0)
                        {
                            Connection.SendMessage($"You deal {damage} damage, a glancing blow.\r\n");
                            await currentRoom.SendMessageToAllPlayers($"{Name} deals {damage} damage to {CurrentTarget.Name}, a glancing blow.\r\n", this);
                        }
                        else
                        {
                            Connection.SendMessage($"You deal {damage} damage.\r\n");
                            await currentRoom.SendMessageToAllPlayers($"{Name} deals {damage} damage to {CurrentTarget.Name}.\r\n", this);
                        }
                    }
                    // Update the threat level for the attacking player
                    if (targetNPC != null)
                    {
                        targetNPC.UpdateThreat(this, damage);
                        Console.WriteLine($"Updating threat for {Name} against {CurrentTarget}");
                    }
                }

                

                    // Update the target NPC's current health
                    targetNPC.CurrentHealth -= damage;
// Make sure the target NPC's health doesn't go below 0
                    if (targetNPC.CurrentHealth < 0)
                    {
                        targetNPC.CurrentHealth = 0;
                    }

// Distribute experience points to the player or group that dealt the killing blow
                    if (targetNPC.CurrentHealth == 0)
                    {
                        if (group != null)
                        {
                            targetNPC.DistributeExperienceToGroup(group);
                        }
                        else if (soul != null)
                        {
                            targetNPC.DistributeExperienceToPlayer(soul);
                            // Check if the player earned enough experience points to level up
                            if (soul.CurrentExperience >= soul.MaxExperience)
                            {
                                CheckLevelUp();
                            }
                        }

                        Connection.SendMessage("You won the fight!\r\n");
                        IsAttacking = false;
                        targetNPC.Die();

                        // Check if the player is in room 27 to send the hint message
                        if (CurrentRoomId == 27)
                        {
                            Connection.SendMessage("\r\n\x1b[32mHINT: Your enemy has been defeated! Type 'look' to see its death status in the room information. Corpses will decompose over time before vanishing. To loot a corpse, type 'loot <target name>'.\x1b[0m\r\n");
                            // Wait for 5 seconds before sending the additional message
                            await Task.Delay(TimeSpan.FromSeconds(5));
                            Connection.SendMessage("\r\n\x1b[34mDream Guardian Di`Zolpem says, 'Hmmm.... you're different... not like the... other ones. You may... pass..'\x1b[0m\r\n");
                        }

                        // Schedule the NPC to respawn
                        DateTime respawnTime = DateTime.UtcNow.AddSeconds(targetNPC.RespawnTimer);
                        _server.NpcRespawnTimers.TryAdd(targetNPC.ID, respawnTime);
                        _attackMessageTokenSource.Cancel();
                    }

                // Wait for the calculated delay time before sending the next message
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayTime), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, cancellationToken);
    }
    else
    {
        // If the player is not attacking, cancel any previous AttackMessages task
        _attackMessageTokenSource?.Cancel();
    }
}
    
    private int CalculateSkillCap(int level)
    {
        return 5 * level;
    }


public async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() => Connection.ReceiveMessage(), cancellationToken);
    }
    catch (OperationCanceledException)
    {
        // Rethrow the exception to be caught by the calling method
        throw;
    }
}


private void CheckLevelUp()
{
    while (CurrentExperience >= MaxExperience)
    {
        if (!CanSkillUp)
        {
            // Reset current experience and increase level
            CurrentExperience -= MaxExperience;
            Level++;
            // Calculate the new max experience based on the player's new level
            MaxExperience = (int)(100 * Math.Pow(Level, 1.5));
            // Send a level up message to the player
            Connection.SendMessage($"You have reached level {Level}!\r\n");
            // Send a skill up available message to the player
            Connection.SendMessage($"You have a skillup point available! Type 'skills' to see the available skills, and 'skillup <skill name>' to gain a point in your chosen skill.\r\n");
            Connection.SendMessage($"Note: You won't be able to gain any more experience levels until you spend your skillup point\r\n");
            //set the canskillup flag true
            CanSkillUp = true;
        }
        else
        {
            // Set the current experience to 1 point below the max experience value for the player
            CurrentExperience = MaxExperience - 1;
            // Send a message to the player informing them about spending their skillup point
            Connection.SendMessage($"You need to spend your skillup point before you can gain another experience level.\r\n");
            Connection.SendMessage($"Type 'skills' to see the available skills, and 'skillup <skill name>' to gain a point in your chosen skill.\r\n");
        }
    }
    Connection.SendMessage($"XP: {ClientConnection.GenerateProgressBar(CurrentExperience, MaxExperience, 20, ConsoleColor.Magenta)} ({CurrentExperience}/{MaxExperience})\r\n");
}


public void IncreaseSkill(string skillToIncrease, int currentSkillValue, double baseChance, int playerLevel)
{
    double maxSkillValue = 200.0;
    Console.WriteLine($"Entering IncreaseSkill for skill: {skillToIncrease}"); // Debug message

    int skillCap = CalculateSkillCap(playerLevel);

    // Calculate the chance based on the current skill value
    double chance;
    if (currentSkillValue <= 1)
    {
        chance = 1.0;
    }
    else if (currentSkillValue >= 2 && currentSkillValue < 100)
    {
        chance = 0.5 - (0.3 * ((currentSkillValue - 2) / 98.0));
    }
    else
    {
        chance = baseChance;
    }

    // Generate a random number and compare it to the calculated chance
    Random random = new Random();
    double randomNumber = random.NextDouble();

    // Check if the random number is less than the chance, the skill value is less than the maximum possible value, and the skill value is below the skill cap
    if (randomNumber < chance && currentSkillValue < maxSkillValue && currentSkillValue < skillCap)
    {
        // Update the player's skill value
        Skills[skillToIncrease].CurrentValue += 1;
        Connection.SendMessage($"Your {skillToIncrease} skill has increased by 1 point.\r\n");
        _server.SaveSoul(this);
    }
    Console.WriteLine($"Exiting IncreaseSkill for skill: {skillToIncrease}"); // Debug message
}


public async Task HandlePlayerDeath(Server server)
{
    if (CurrentHealth <= 0)
    {
        // Send a message to the player
        await Connection.SendMessage("\x1b[31mYou have died!\x1b[0m\r\n");

        // Calculate experience loss
        double experienceLossPercentage = Math.Min(0.1, 0.01 * Level);
        int experienceLoss = (int)(CurrentExperience * experienceLossPercentage);
        CurrentExperience -= experienceLoss;
        if (CurrentExperience < 0)
        {
            CurrentExperience = 0;
        }

        // Move the player to the recall room
        CurrentRoomId = RecallRoom;
        Room recallRoom = server.GetRoomById(RecallRoom);
        if (recallRoom != null)
        {
            CurrentRoomId = RecallRoom;
        }

        // Restore half of max health and mana
        CurrentHealth = MaxHealth / 2;
        CurrentMana = MaxMana / 2;

        // Set IsAttacking to false
        IsAttacking = false;
    }
}



public void EquipItem(Item itemToEquip)
{
    if (itemToEquip == null)
    {
        throw new ArgumentNullException(nameof(itemToEquip), "Item cannot be null.");
    }

    int slotNumber = itemToEquip.SlotNumber;
    if (slotNumber < 1 || slotNumber > 19)
    {
        throw new ArgumentException("Invalid slot number.");
    }

    // Check if there's already an item equipped in the same slot
    if (Equipment.TryGetValue(slotNumber, out Item equippedItem) && equippedItem != null)
    {
        throw new ArgumentException($"You already have an item equipped in the {itemToEquip.Slot} slot.");
    }

    Equipment[slotNumber] = itemToEquip;

    Console.WriteLine($"Equipping '{itemToEquip.Name}' in slot {itemToEquip.SlotNumber}.");
    Console.WriteLine("Added attribute values:");

    Console.WriteLine($"str: {itemToEquip.Str}");
    Console.WriteLine($"dex: {itemToEquip.Dex}");
    Console.WriteLine($"wis: {itemToEquip.Wis}");
    Console.WriteLine($"int: {itemToEquip.Int}");
    Console.WriteLine($"con: {itemToEquip.Con}");
    Console.WriteLine($"cha: {itemToEquip.Cha}");
}



        public void UnequipItem(int slotNumber)
        {
            if (slotNumber >= 1 && slotNumber <= 19)
            {
                if (Equipment.TryGetValue(slotNumber, out Item itemToUnequip) && itemToUnequip != null)
                {
                    // Find the first available slot in the inventory
                    int availableSlot = -1;
                    for (int i = 1; i <= maxInventorySlots; i++)
                    {
                        if (!Inventory.ContainsKey(i))
                        {
                            availableSlot = i;
                            break;
                        }
                    }

                    // Add the item back to the inventory if there is an available slot
                    if (availableSlot != -1)
                    {
                        Inventory.Add(availableSlot, itemToUnequip);
                    }
                    else
                    {
                        // Handle the case when the inventory is full, if necessary
                    }

                    // Set the equipment slot to null
                    Equipment[slotNumber] = null;
                }
            }
        }

        
        public async Task CheckClassChange()
        {
            Dictionary<PlayerClass, int> qualifyingClasses = new Dictionary<PlayerClass, int>();

// Check for qualifying classes and store their primary skill values
            if (Skills["Faith"].CurrentValue >= 3)
            {
                qualifyingClasses[new Priest()] = Skills["Faith"].CurrentValue;
            }

            if (Skills["Etherealism"].CurrentValue >= 5)
            {
                qualifyingClasses[new Etherealist()] = Skills["Etherealism"].CurrentValue;
            }

            if (Skills["Stealth"].CurrentValue >= 3)
            {
                qualifyingClasses[new Rogue()] = Skills["Stealth"].CurrentValue;
            }
            if (Skills["Sagacity"].CurrentValue >= 3)
            {
                qualifyingClasses[new Sage()] = Skills["Sagacity"].CurrentValue;
            }
            if (Skills["Occultism"].CurrentValue >= 3)
            {
                qualifyingClasses[new Occultist()] = Skills["Occultism"].CurrentValue;
            }
            if (Skills["Arcanism"].CurrentValue >= 3)
            {
                qualifyingClasses[new Wizard()] = Skills["Arcanism"].CurrentValue;
            }
            if (Skills["Hand to Hand"].CurrentValue >= 10)
            {
                qualifyingClasses[new Brawler()] = Skills["Hand to Hand"].CurrentValue;
            }
            if (Skills["Slashing"].CurrentValue >= 10)
            {
                qualifyingClasses[new Swordsman()] = Skills["Slashing"].CurrentValue;
            }
            if (Skills["Psionics"].CurrentValue >= 20)
            {
                qualifyingClasses[new Psychic()] = Skills["Psionics"].CurrentValue;
            }
            if (Skills["Vampirism"].CurrentValue >= 3)
            {
                qualifyingClasses[new Vampire()] = Skills["Vampirism"].CurrentValue;
            }
            if (Skills["Illusion"].CurrentValue >= 3)
            {
                qualifyingClasses[new Illusionist()] = Skills["Illusion"].CurrentValue;
            }
            if (Skills["Hedgecrafting"].CurrentValue >= 3)
            {
                qualifyingClasses[new Hedgecrafter()] = Skills["Hedgecrafting"].CurrentValue;
            }

// If only one class is qualified, set class to that class
            if (qualifyingClasses.Count == 1)
            {
                TryChangeClass(qualifyingClasses.Keys.First());
                bool wasNight = _server.IsNight;
                _server.IsNight = Connection.CheckIfNight();

                if (wasNight != _server.IsNight)
                {
                    await Connection.ApplyVampireBonusAsync(_server.IsNight);
                }

            }
// If multiple classes are qualified, determine the highest skill value within those classes
            else if (qualifyingClasses.Count > 1)
            {
                KeyValuePair<PlayerClass, int> highestSkillClass =
                    qualifyingClasses.Aggregate((l, r) => l.Value > r.Value ? l : r);
                TryChangeClass(highestSkillClass.Key);
            }
            else
            {
                TryChangeClass(new Traveler());
            }
        }


        
        
        public void TryChangeClass(PlayerClass newClass)
        {
            if (newClass.CanBecomeClass(this))
            {
                string oldClassName = currentClass; // Store current class name in a temporary variable
                newClass.ApplyClass(this);
                if (oldClassName != newClass.Name)
                {
                    currentClass = newClass.Name; // Update current class name
                   //Connection.SendMessage($"You are now a {newClass.Name}!");
                }
                else
                {
                    //Console.WriteLine($"{Name} remains a {newClass.Name}.");
                }
            }
        }



        
        public void LoadClassFromDatabase(SQLiteConnection connection)
        {
            using (var command = new SQLiteCommand("SELECT ClassName FROM Souls WHERE Name = @name", connection))
            {
                command.Parameters.AddWithValue("@name", Name);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var className = reader.GetString(0);
                        CurrentClass = PlayerClass.GetPlayerClassByName(className);
                    }
                }
            }
        }
        
    }
}
