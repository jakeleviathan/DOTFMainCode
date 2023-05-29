using System;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SanctuaryMUD.Classes;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace SanctuaryMUD


{
    public class ClientConnection
    {
        private TcpClient _client;
        private Server _server;
        private StreamWriter _writer;
        private StreamReader _reader;
        private static SHA256 _sha256 = SHA256.Create();
        private SemaphoreSlim _readWriteSemaphore = new SemaphoreSlim(1, 1);
        private Dictionary<string, Item> _itemsBeingCreated = new Dictionary<string, Item>();
        private SemaphoreSlim _commandSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource ImpcoinCancellationTokenSource { get; set; }
        public Timer ImpcoinTimer { get; set; }
        private Soul _soul;
        private const int maxInventorySlots = 20;
        public Soul Soul { get; set; }
        private CancellationTokenSource _cts;
        public List<int> GetPossibleRewardItemIds()
        {
            // Replace this with the actual code to fetch the list of item IDs from the database
            // For example, you can use a query to fetch all item IDs with a specific flag or item type
            List<int> possibleRewardItemIds = new List<int> { 1001, 1002, 1003, 1004, 1005 };
            return possibleRewardItemIds;
        }

        public bool IsConnected => _client.Connected;
        public DateTime LastActive { get; set; }
        private CraftingManager _craftingManager;
        private ManualResetEvent _inputReceivedEvent = new ManualResetEvent(false);
        private volatile string _receivedInput;
        private readonly TaskCompletionSource<string> _inputTcs = new TaskCompletionSource<string>();
        private readonly Channel<string> _inputChannel = Channel.CreateUnbounded<string>();
        private CancellationTokenSource _inputTimeoutCts;
        public DateTime LastActivity { get; set; }
        private Timer _idleTimer;
        public event EventHandler Disconnected;
        List<string> colors = new List<string> {
            "255;105;180", // Hot Pink
            "173;216;230", // Light Blue
            "255;20;147",  // Deep Pink
            "70;130;180",  // Steel Blue
            "255;165;0",   // Orange
            "75;0;130",    // Indigo
            "255;255;0",   // Yellow
            "0;255;0",     // Lime
            "128;0;128",   // Purple
            "255;0;255",   // Fuchsia
            "0;128;0",     // Green
            "128;128;0",   // Olive
            "0;0;255",     // Blue
            "128;0;0",     // Maroon
            "0;128;128",   // Teal
            "0;0;128",     // Navy
            "255;0;0",     // Red
            "255;140;0",   // Dark Orange
            "255;215;0",   // Gold
            "240;230;140", // Khaki
            "189;183;107", // Dark Khaki
            "218;165;32",  // Goldenrod
            "184;134;11",  // Dark Goldenrod
            "205;133;63",  // Peru
            "139;69;19",   // Saddle Brown
            "244;164;96",  // Sandy Brown
            "210;180;140", // Tan
            "210;105;30",  // Chocolate
            "139;0;0",     // Dark Red
            "165;42;42",   // Brown
            "178;34;34",   // Firebrick
            "255;69;0",    // Orange Red
            "255;99;71",   // Tomato
            "233;150;122", // Dark Salmon
            "250;128;114", // Salmon
            "255;160;122", // Light Salmon
            "255;182;193", // Light Pink
            "255;192;203", // Pink
            "221;160;221", // Plum
            "218;112;214", // Orchid
            "186;85;211",  // Medium Orchid
            "153;50;204",  // Dark Orchid
            "148;0;211",   // Dark Violet
            "138;43;226",  // Blue Violet
            "102;51;153",  // Rebecca Purple
            "75;0;130",    // Indigo
            "123;104;238", // Medium Slate Blue
            "106;90;205",  // Slate Blue
            "72;61;139",   // Dark Slate Blue
            "147;112;219", // Medium Purple
            "139;0;139",   // Dark Magenta
            "128;0;128",   // Purple
            "199;21;133",  // Medium Violet Red
            "219;112;147", // Pale Violet Red
            "176;48;96",   // Indian Red
            "255;20;147",  // Deep Pink
            "255;105;180", // Hot Pink
            "124;252;0",   // Lawn Green
            "50;205;50",   // Lime Green
        };


        private enum ItemCreationState
        {
            Slot,
            SlotNumber,
            Str,
            Dex,
            Wis,
            Int,
            Con,
            Cha,
            Type,
            Alias,
            Fashion,
            Description,
            Done
        }

        private Dictionary<string, ItemCreationState> _itemCreationStates = new Dictionary<string, ItemCreationState>();
        private static readonly List<string> StatNames = new List<string> { "str", "dex", "int", "con", "wis", "cha" };



        private static readonly Dictionary<string, string> HelpTopics = new Dictionary<string, string>
        {
            { "who", "Displays online souls. Optional: 'who <soul name>'" },
            { "look", "Displays room information" },
            { "say", "Speaks in the local room" },
            { "tell", "Speaks directly to another soul: 'tell <soul name>'" },
            { "inv", "Views inventory" },
            { "eq", "Views equipment" },
            { "equip", "Equips something: 'equip <item name>'" },
            { "id", "Views information about an item: 'id <item name>' or 'examine <item name>'" },
            { "unequip", "Unequips something: 'unequip <slot number>'" },
            { "cast", "Casts a spell: 'cast <spell name>'" },
            { "n", "Move north: 'n' or 'north'" },
            { "e", "Move east: 'e' or 'east'" },
            { "s", "Move south: 's' or 'south'" },
            { "w", "Move west: 'w' or 'west'" },
            { "afk", "Show as AFK to other players in the 'who' list" },
            { "bio", "Set your soul's short 12 character bio in the 'who' list: 'bio <bio text>'" },
            { "soul", "View information about your soul, such as your stats" },
            { "emote", "Perform an emote in the current room: 'emote <action>'" },
            { "hide", "Attempt to hide from other players in the current room" },
            { "unhide", "Unhide yourself from view" },
            { "anon", "Go anonymous. Anonymous players show as 'Someone' and 'Anonymous' in the 'who' list" },
            { "graffiti", "Tag a message up to 12 characters in a room: 'graffiti <graffiti text>'" },
            { "clean", "Remove graffiti from the current room" },
            { "con", "View information about an NPC: 'con <npc name>'" },
            { "age", "Set your soul's age" },
            { "gift", "Enter a promotional gift code: 'gift <gift code>'" },
            { "drop", "Drop an item from your inventory: 'drop <slot number>'" },
            { "enter portal", "Enter a portal in the current room to travel to another room" },
            {
                "soulbind",
                "Set your spirit recall and respawn location to the current room (requires a Soulbind Statue present)"
            },
            { "invite", "Sends a group invite to your target: 'invite <player name>'" },
            { "accept group", "Accepts a group invite and joins the group" },
            { "group", "Speaks in group chat: 'group or g <message>'" },
            { "inspect", "Look more closely at an object in the room: 'inspect or ins <object name>'" },
            { "leave group", "Leave your current group" },
            { "disband", "Disband your current group if you are the group leader" }
        };

        private static readonly Dictionary<string, List<string>> HelpCategories = new Dictionary<string, List<string>>
        {
            { "Communication", new List<string> { "who", "say", "tell", "emote", "group" } },
            { "Movement", new List<string> { "n", "e", "s", "w" } },
            { "Character Information", new List<string> { "look", "inv", "eq", "soul", "age", "bio" } },
            { "Items and Inventory", new List<string> { "equip", "id", "unequip", "drop" } },
            { "Spells and Abilities", new List<string> { "cast" } },
            {
                "Exploration and Interaction",
                new List<string> { "inspect", "enter portal", "soulbind", "con", "graffiti", "clean" }
            },
            { "Player Status", new List<string> { "afk", "hide", "unhide", "anon" } },
            { "Grouping", new List<string> { "invite", "accept group" } },
            { "Miscellaneous", new List<string> { "gift" } },
        };
        Random rand = new Random();

        string GetRandomColorCode()
        {
            int randomIndex = rand.Next(colors.Count);
            return colors[randomIndex];
        }

        string ColorizeName(string name)
        {
            string colorizedName = "";
            foreach (char c in name)
            {
                colorizedName += $"\x1b[38;2;{GetRandomColorCode()}m{c}\x1b[0m";
            }
            return colorizedName;
        }
        public async Task ApplyVampireBonusAsync(bool isNight)
        {
            foreach (var connectedPlayer in _server.Souls)
            {
                if (Soul.CurrentClass.Name.Equals("Vampire", StringComparison.OrdinalIgnoreCase))
                {
                    if (isNight)
                    {
                        connectedPlayer.MaxHealth = (int)(connectedPlayer.MaxHealth * 1.1);
                        connectedPlayer.MaxMana = (int)(connectedPlayer.MaxMana * 1.1);
                        await SendMessage("\r\nNight falls, and your thirst for blood grows stronger.\r\n");
                    }
                    else
                    {
                        connectedPlayer.MaxHealth = (int)(connectedPlayer.MaxHealth / 1.1);
                        connectedPlayer.MaxMana = (int)(connectedPlayer.MaxMana / 1.1);
                        await SendMessage("The sun rises, weakening your power.\r\n");
                    }

                    _server.SaveSoul(connectedPlayer);
                }
            }
        }

        public ClientConnection(TcpClient client, Server server)
        {
            try
            {
                _client = client;
                _server = server;
                _writer = new StreamWriter(_client.GetStream(), Encoding.ASCII);
                _reader = new StreamReader(_client.GetStream(), Encoding.ASCII);
                _craftingManager = new CraftingManager(IncreaseSkill);
                Console.WriteLine("CraftingManager instance created."); // Debug message
                LastActivity = DateTime.UtcNow;
                _idleTimer = new Timer(CheckIdleTime, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));


            }
            catch (Exception ex)
            {
                // Handle the exception if the client forcefully disconnects
                if (ex is IOException || ex is SocketException)
                {
                    PlayerLoggedOut(Soul);
                }
            }
        }


       




        private void IncreaseSkill(string skillToIncrease, int currentSkillValue, double baseChance)
        {
            // Assume the player instance is called '_player'
            _soul.IncreaseSkill(skillToIncrease, currentSkillValue, baseChance, Soul.Level);
        }

        // Call this method when the player gets disconnected
        public void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private async void CheckIdleTime(object state)
        {
            try
            {
                var soulName = Soul?.Name ?? "Unknown";
                var lastActivity = LastActivity;
                TimeSpan idleTime = DateTime.UtcNow - lastActivity;

                if (idleTime >= TimeSpan.FromMinutes(5) && idleTime < TimeSpan.FromMinutes(10))
                {
                    // Check if the player is not a GM before sending the message
                    if (Soul == null || !Soul.IsGM)
                    {
                        await SendMessage(
                            "Your Astral Link to Magnos is unstable... enter any command to remain connected.....\r\n");
                    }
                }
                else if (idleTime >= TimeSpan.FromMinutes(10))
                {
                    if (Soul != null && Soul.IsGM)
                    {
                        Console.WriteLine($"{soulName} is a GM and has been idle for more than 10 minutes. Ignoring disconnection...");
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"{soulName} has been idle for more than 10 minutes. Disconnecting...");
                        Disconnect();
                        OnDisconnected();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckIdleTime: {ex.Message}");
            }
        }



        public async Task HandleClient()
        {
            try
            {
                Soul playerSoul = null;
                _cts = new CancellationTokenSource();

                async Task GrantDailyLoginReward(Soul soul)
                {
                    if (DateTime.Now.Subtract(soul.LastLogin).TotalHours >= 24)
                    {
                        soul.Impcoins += 5;
                        await SendMessage("\r\n\x1b[92mYou received a daily login bonus of 5 Impcoins!\x1b[0m\r\n");
                    }
                }

                try
                {
                    SendWelcomeScreen();

                    bool loggedIn = false;
                    while (!loggedIn)
                    {
                        await SendMessage("\x1b[38;2;255;105;180m   *~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                        await SendMessage(WrapText("\x1b[36m                    Do you have an existing soul? (Y/N)\x1b[0m", 170) + "\r\n\r\n");
                        string input = await ReadLineAsync(_cts.Token);
                        Console.WriteLine($"Received input: {input}");
                        if (input?.ToLower() == "y")
                        {
                            // Prompt the player for their soul name and password
                            await SendMessage("Soul Name: ");
                            string soulName = await ReadLineAsync(_cts.Token);
                            await SendMessage("Password: ");
                            string password = await ReadLineAsync(_cts.Token);
                            Console.WriteLine($"Soul name: {soulName}, Password: {password}");
                            Console.WriteLine($"Received soul name: {soulName}, Password: {password}");

                            playerSoul = await _server.LoadSoul(soulName, password);
                            if (playerSoul != null)
                            {
                                // Check if the soul is already logged in
                                if (_server.Souls.Any(s =>
                                        s.Name.Equals(playerSoul.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    // Send a message to the client informing them that the soul is already logged in
                                    await SendMessage(
                                        "This soul is already logged in. Please log out the other session before logging in again.\r\n");
                                    return;
                                }

                                loggedIn = true; // Set loggedIn to true, as the player has successfully logged in
                                this.Soul = playerSoul;
                                playerSoul.Connection = this;
                                _server.Souls.Add(playerSoul);
                                Console.WriteLine("Existing soul added to server");

                                await SendWarpEffect();
                                await Task.Delay(TimeSpan.FromSeconds(1));
                                // Get the current room of the Soul
                                Room currentRoom = _server.GetRoomById(playerSoul.CurrentRoomId);
                                if (currentRoom == null)
                                {
                                    // If the current room is not found, add the Soul to the starting room
                                    currentRoom = _server.GetStartingRoom();
                                }

                                currentRoom.AddPlayerToRoom(playerSoul, _server, currentRoom);
                                await DisplayRoomInformation(playerSoul, currentRoom);
                                await GrantDailyLoginReward(playerSoul);
                                Task.Run(() => HandlePlayerCommands(playerSoul));
                                await PlayerLoggedIn(playerSoul);

                            }
                            else
                            {
                                Console.WriteLine("Failed to read from the reader or no matching soul found.");
                                // Invalid login credentials
                                await SendMessage("Invalid soul name or password.\n\r> ");
                            }
                        }
                        else
                        {
                            // Prompt the player to create a new soul
                            await SendMessage(
                                WrapText(
                                    "To initialize the Astral Link's E.A.R.T.H. protocol and enter Magnos, you must choose a soul name.",
                                    70) + "\r\n\r\n");

                            bool validName = false;
                            string soulName;

                            do
                            {
                                await SendMessage("Soul name: ");
                                soulName = await ReadLineAsync(_cts.Token);

                                if (IsValidSoulName(soulName))
                                {
                                    validName = true;
                                }
                                else
                                {
                                    await SendMessage(
                                        "\x1b[91mInvalid soul name. Please follow the naming rules and choose a unique name.\x1b[0m\r\nNaming Rules: Names must contain alphabetical letters only and begin with a capital letter. Names cannot contain numbers or spaces.\r\n");
                                }
                            } while (!validName);

                            await SendMessage("Select a password.\n\r\n");
                            await SendMessage("Password: ");
                            string password = await ReadLineAsync(_cts.Token);
                            Race selectedRace = await PromptRaceSelection();
// Add age selection
                            int age = 0;
                            bool validAge = false;
                            do
                            {
                                await SendMessage("Enter your age (between 12 and 120): ");
                                string ageInput = await ReadLineAsync(_cts.Token);

                                if (int.TryParse(ageInput, out age) && age >= 12 && age <= 120)
                                {
                                    validAge = true;
                                }
                                else
                                {
                                    await SendMessage(
                                        "\x1b[91mInvalid age. Please enter a number between 12 and 120.\x1b[0m\r\n");
                                }
                            } while (!validAge);

                            if (selectedRace != null)
                            {
                                // Generate a random salt
                                byte[] saltBytes = new byte[32];
                                using (var rng = new RNGCryptoServiceProvider())
                                {
                                    rng.GetBytes(saltBytes);
                                }

                                string salt = Convert.ToBase64String(saltBytes);
// Use the salt when calling GetPasswordHash
                                Console.WriteLine($"New soul password: {password}");
                                Soul newSoul = new Soul(_server, soulName, GetPasswordHash(password, salt),
                                    selectedRace, salt, age,
                                    selectedRace.MaxHealth, selectedRace.MaxMana, selectedRace.MaxHealth,
                                    selectedRace.MaxMana,
                                    0, 1, 1, false, DateTime.UtcNow, "Newbie", CreateDefaultSkills(), 7, 0);
                                using (var connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
                                {
                                    connection.Open();
                                    string passwordHash = GetPasswordHash(password, salt);
                                    Console.WriteLine(
                                        $"Saved new soul to the database: Name: {soulName}, Password: {password}, PasswordHash: {passwordHash}, Salt: {salt}");
                                    Console.WriteLine(
                                        $"Created new soul with PasswordHash: {passwordHash}, Salt: {salt}");
                                    using (var command = connection.CreateCommand())
                                    {
                                        command.CommandText =
                                            "INSERT INTO Souls (Name, PasswordHash, Salt, Race, Age, Level, CurrentRoomId, IsGM, RecallRoom) VALUES (@Name, @PasswordHash, @Salt, @Race, @Age, @Level, @CurrentRoomId, @IsGM, @RecallRoom)";
                                        command.Parameters.AddWithValue("@Name", soulName);
                                        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                                        command.Parameters.AddWithValue("@Salt", salt);
                                        command.Parameters.AddWithValue("@Race", selectedRace.RaceType.ToString());
                                        command.Parameters.AddWithValue("@Age", age);
                                        command.Parameters.AddWithValue("@Level", 1);
                                        command.Parameters.AddWithValue("@CurrentRoomId", 7);
                                        command.Parameters.AddWithValue("@IsGM", 0);
                                        command.Parameters.AddWithValue("@RecallRoom", 7);
                                        command.ExecuteNonQuery();
                                    }
                                }

                                loggedIn = true;
                                await SendMessage($"Welcome, {newSoul.Name} the {newSoul.Race.RaceName}!\n");
                                _server.Souls.Add(newSoul);
                                _server.CreateSampleItems(
                                    newSoul); // Call the CreateSampleItems method to add sample items to the inventory
                                Console.WriteLine("New soul added to the server.");
                                this.Soul = newSoul;
                                newSoul.Connection = this;
                                newSoul.CurrentClass = new Traveler(); // Set the current class to Traveler
                                await PlayerLoggedIn(newSoul);
                                await SendWarpEffect();
                                Console.WriteLine("Warp effect sent.");
                                Room startingRoom = _server.GetStartingRoom();
                                startingRoom.AddPlayerToRoom(newSoul, _server, startingRoom);
                                await DisplayRoomInformation(newSoul, startingRoom);
                                await GrantDailyLoginReward(newSoul);
                                Task.Run(() => HandlePlayerCommands(newSoul));

                            }

                            else
                            {
                                await SendMessage("Invalid selection. Please try again.\n");
                                return;
                            }
                        }
                    }
                }

                catch (Exception ex) when (ex is IOException || ex is OperationCanceledException)
                {
                    Console.WriteLine("Client forcefully disconnected: " + ex.Message);
                    await PlayerLoggedOut(Soul);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception caught: " + ex.Message);
                    _client.Close();
                }
            }
            catch (Exception ex)
            {
                // Handle the exception if the client forcefully disconnects
                if (ex is IOException || ex is SocketException)
                {
                    await PlayerLoggedOut(Soul);
                }
            }
        }

        private async Task HandleClientDisconnection(Soul playerSoul)
        {
            // Save the player data
            _server.SaveSoul(Soul);

            // Remove player from the room
            Room currentRoom = _server.GetRoomById(Soul.CurrentRoomId);
            currentRoom.RemovePlayerFromRoom(Soul);

            // Remove player from the online player list
            _server.Souls.Remove(Soul);

            // Handle logout functions
            await PlayerLoggedOut(Soul);

            // Remove the client from the connected clients list
            _server.RemoveClient(this);

            // Close the connection
            _client.Close();

        }

        public async Task DisplayRaceInformation(Race race)
        {
            await SendMessage("\r\n" + "\x1b[95m" + WrapText($"{race.RaceName}", 70) + "\r\n\x1b[0m");
            await SendMessage(WrapText($"{race.RaceDescription}", 70) + "\r\n\r\n\r\n");
            await SendMessage("\r\n" + "\x1b[33m" + WrapText($"Special Ability: {race.SpecialAbility}", 70) +
                              "\r\n\x1b[0m");
            await SendMessage(WrapText($"{race.SpecialAbilityDescription}", 70) + "\r\n");
            await SendMessage("\r\n" + "\x1b[91;1m" + WrapText($"Starting HP: {race.MaxHealth}", 70) + "\x1b[0m");
            await SendMessage("\r\n" + "\x1b[96m" + WrapText($"Starting Mana: {race.MaxMana}", 70) + "\r\n\x1b[0m");
            await SendMessage("\r\n");
        }

        public async Task DisplaySoulInformation(Soul soul)
        {
            Room recallRoom = _server.GetRoomById(soul.RecallRoom);

            // Add the top border
            await SendMessage(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");
            string title = "Soul Information";
            int padding = (80 - title.Length) / 2;
            string centeredTitle = title.PadLeft(title.Length + padding).PadRight(80);
            await SendMessage($"\x1b[36m{centeredTitle}\x1b[0m\r\n");
            StringBuilder soulInfoMessage = new StringBuilder("\r\n");
            soulInfoMessage.AppendFormat("{0, -20} {1, -20} {2}\r\n", $"Name: {soul.Name}", $"Race: {soul.Race.RaceName}", $"Class: {Soul.GetColoredClassName(soul.CurrentClass.Name)}");
            soulInfoMessage.AppendFormat("{0, -20} {1, -20} {2}\r\n", $"Level: {soul.Level}", $"Age: {soul.Age} years", $"Soulbind Location: {recallRoom.Name}");
            soulInfoMessage.AppendFormat("{0, -20} {1, -20} {2}\r\n", $"Health: {soul.CurrentHealth}/{soul.GetTotalMaxHealth()}", $"Mana: {soul.CurrentMana}/{soul.GetTotalMaxMana()}", $"Experience: {soul.CurrentExperience}/{soul.MaxExperience}");
            soulInfoMessage.AppendLine("\r\n\x1b[36mStats:\x1b[0m");
            foreach (KeyValuePair<string, int> attribute in soul.Attributes)
            {
                int totalAttributeValue = soul.GetTotalAttributeValue(attribute.Key);
                soulInfoMessage.AppendFormat("{0, -15}: {1}\r\n", attribute.Key, totalAttributeValue);
            }
            // Display active effects
            soulInfoMessage.AppendLine($"\r\n\x1b[36mEffects: \x1b[0m");


            
            await SendMessage(soulInfoMessage.ToString());



            // Add the "moonlit dodge" effect for moon elf players
            if (soul.Race.RaceName.Equals("moon elf", StringComparison.OrdinalIgnoreCase))
            {
                soul.ActiveEffects.Add("moonlit dodge");
            }

            // Add the "magical blood" effect for mana elf players
            if (soul.Race.RaceName.Equals("mana elf", StringComparison.OrdinalIgnoreCase))
            {
                soul.ActiveEffects.Add("magical blood");
            }

            if (soul.ActiveEffects.Count > 0)
            {
                foreach (var effect in soul.ActiveEffects)
                {
                    soulInfoMessage.AppendLine($"{effect}");
                }
            }
            else
            {
                soulInfoMessage.AppendLine("No active effects.");
            }

            // Remove the "moonlit dodge" effect after displaying it for moon elf players
            if (soul.Race.RaceName.Equals("moon elf", StringComparison.OrdinalIgnoreCase))
            {
                soul.ActiveEffects.Remove("moonlit dodge");
            }

            // Remove the "magical blood" effect after displaying it for mana elf players
            if (soul.Race.RaceName.Equals("mana elf", StringComparison.OrdinalIgnoreCase))
            {
                soul.ActiveEffects.Remove("magical blood");
            }
            if (soul.ActiveEffects.Count > 0)
            {
                foreach (var effect in soul.ActiveEffects)
                {
                    await SendMessage($"{effect}\r\n");
                }
            }
            else
            {
                await SendMessage("No active effects.\r\n");
            }
            // Add the bottom border
            await SendMessage(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
        }




        public static Dictionary<string, Skill> CreateDefaultSkills()
        {
            return new Dictionary<string, Skill>
            {
                { "Slashing", new Skill("Slashing", 0) },
                { "Piercing", new Skill("Piercing", 0) },
                { "Blunt", new Skill("Blunt", 0) },
                { "Hand to Hand", new Skill("Hand to Hand", 0) },
                { "Foraging", new Skill("Foraging", 0) },
                { "Hedgecrafting", new Skill("Hedgecrafting", 0) },
                { "Geomancy", new Skill("Geomancy", 0) },
                { "Occultism", new Skill("Occultism", 0) },
                { "Faith", new Skill("Faith", 0) },
                { "Demonology", new Skill("Demonlogy", 0) },
                { "Jewelcrafting", new Skill("Jewelcrafting", 0) },
                { "Tailoring", new Skill("Tailoring", 0) },
                { "Smithing", new Skill("Smithing", 0) },
                { "Cooking", new Skill("Cooking", 0) },
                { "Brewing", new Skill("Brewing", 0) },
                { "Alchemy", new Skill("Alchemy", 0) },
                { "Crafting", new Skill("Crafting", 0) },
                { "Selling", new Skill("Selling", 0) },
                { "Vampirism", new Skill("Vampirism", 0) },
                { "Shifting", new Skill("Shifting", 0) },
                { "Philosophy", new Skill("Philosophy", 0) },
                { "Woundtending", new Skill("Woundtending", 0) },
                { "Illusion", new Skill("Illusion", 0) },
                { "Voodoo", new Skill("Voodoo", 0) },
                { "Stealth", new Skill("Stealth", 0) },
                { "Necromancy", new Skill("Necromancy", 0) },
                { "Etherealism", new Skill("Etherealism", 0) },
                { "Galaxy Magic", new Skill("Galaxy Magic", 0) },
                { "White Magic", new Skill("White Magic", 0) },
                { "Black Magic", new Skill("Black Magic", 0) },
                { "Blue Magic", new Skill("Blue Magic", 0) },
                { "Blood Magic", new Skill("Blood Magic", 0) },
                { "Sagacity", new Skill("Sagacity", 0) },
                { "Arcanism", new Skill("Arcanism", 0) },
                { "Mining", new Skill("Mining", 0) },
                { "Forestry", new Skill("Forestry", 0) },
                { "Harvesting", new Skill("Harvesting", 0) },
                { "Advanced Crafting", new Skill("Advanced Crafting", 0) },
                { "Psionics", new Skill("Psionics", 0) },
                { "Engineering", new Skill("Engineering", 0) },
                { "Hacking", new Skill("Hacking", 0) },
                { "Soulwork", new Skill("Soulwork", 0) }
                
                

            };
        }

        private async Task ReadAndSendLineAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string input = await ReadLineAsync(cancellationToken);
                await _inputChannel.Writer.WriteAsync(input, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation if needed
            }
        }


        public async Task HandlePlayerCommands(Soul soul, string receivedInput = null)
        {
            Item itemBeingCreated = null;
            Queue<string> inputQueue = new Queue<string>();

            while (true)
            {
                if (inputQueue.Count == 0)
                {
                    string newInput = await ReadLineAsync(_cts.Token);
                    inputQueue.Enqueue(newInput);
                }

                string currentInput = inputQueue.Dequeue();
                Console.WriteLine("Waiting for semaphore...");
                await _commandSemaphore.WaitAsync();
                Console.WriteLine("Semaphore acquired...");
                string[] inputParts = currentInput.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string command = inputParts.Length > 0 ? inputParts[0].ToLower() : "";
                string argument = inputParts.Length > 1 ? inputParts[1].Trim() : "";
                // Split the input into words
                string[] inputWords = currentInput.Split(' ');

try {
                try
                {
                    Console.WriteLine($"Initial command: {command}");
                    Console.WriteLine($"Received command: {command}");

                    if (command == "soul")
                    {
                        await DisplaySoulInformation(soul);
                    }

                    if (command == "who")
                    {
                        if (inputParts.Length == 1)
                        {
                            await DisplayOnlinePlayers();
                        }
                        else if (inputParts.Length > 1)
                        {
                            string playerName = inputParts[1];
                            Soul targetSoul =
                                _server.Souls.FirstOrDefault(s =>
                                    s.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                            if (targetSoul != null)
                            {
                                Room currentRoom = _server.GetRoomById(targetSoul.CurrentRoomId);
                                string roomName = currentRoom != null ? currentRoom.Name : "Unknown";

                                await SendMessage($"\r\n\x1b[36mSoul Information\x1b[0m\r\n");
                                await SendMessage(
                                    "\x1b[36m---------------------------------------------------------\r\n\x1b[0m");
                                await SendMessage($"Name: {targetSoul.Name}\r\n");
                                await SendMessage($"Race: {targetSoul.Race.RaceName}\r\n");
                                await SendMessage(
                                    $"Class: {Soul.GetColoredClassName(targetSoul.CurrentClass.Name)}\r\n");
                                await SendMessage($"Level: {targetSoul.Level}\r\n");
                                await SendMessage($"Room: {roomName}\r\n");
                                await SendMessage("\r\n ");
                            }

                            else
                            {
                                await SendMessage($"No soul found with the name '{playerName}'.");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: who [playername]");
                        }
                    }

                    if (command == "defile")
                    {
                        if (inputParts.Length >= 2)
                        {
                            string targetName = inputParts[1];
                            await Defile(soul, targetName);
                        }
                        else
                        {
                            await SendMessage("Usage: defile <targetname>\r\n");
                        }
                    }

                    if (command == "level" && soul.IsGM)
                    {
                        if (inputParts.Length > 1)
                        {
                            if (int.TryParse(inputParts[1], out int desiredLevel))
                            {
                                if (desiredLevel > 0)
                                {
                                    soul.Level = desiredLevel;
                                    await SendMessage($"You have set your level to {soul.Level}.\r\n");
                                }
                                else
                                {
                                    await SendMessage("Please enter a valid positive level number.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("Invalid level number. Usage: level <level>\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: level <level>\r\n");
                        }
                    }

                    if (command.StartsWith("forage"))
                    {
                        Forage(soul, soul.CurrentRoomId);
                    }

                    if (command.StartsWith("gaze"))
                    {
                        if (inputParts.Length > 1)
                        {
                            string playerName = inputParts[1];
                            Soul targetSoul = _server.Souls.FirstOrDefault(s =>
                                s.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) &&
                                s.CurrentRoomId == soul.CurrentRoomId);

                            if (targetSoul != null)
                            {
                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

// Calculate the padding needed to center the title
                                int titleLength = 16; // Length of the words "Visual Information"
                                int totalBorderLength = 74; // Length of the border
                                int padding = (totalBorderLength - titleLength) / 2;

// Add the padding and the centered title
                                string paddedTitle = new string(' ', padding) + "\x1b[36mVisual Information\x1b[0m";
                                await SendMessage($"\r\n{paddedTitle}\r\n");

                                if (!string.IsNullOrEmpty(targetSoul.IllusionObject)) // Check if the player has an illusion object
                                {
                                    await SendMessage($"{targetSoul.Name} appears to you as a {targetSoul.IllusionObject}.\r\n");
                                }
                                else
                                {
                                    await SendMessage(
                                        $"{targetSoul.Name} appears to you as a {targetSoul.Age} year old {targetSoul.Race.RaceName}.\r\n");
                                }


                                StringBuilder equipmentDescription = new StringBuilder();
                                for (int i = 1; i <= 19; i++)
                                {
                                    Item fashionItem = null;
                                    Item equipmentItem = null;

                                    if (targetSoul.Fashion.TryGetValue(i.ToString(), out fashionItem) ||
                                        targetSoul.Equipment.TryGetValue(i, out equipmentItem))
                                    {
                                        if (fashionItem != null)
                                        {
                                            if (i >= 1 && i <= 15)
                                            {
                                                equipmentDescription.AppendLine(
                                                    $"They are wearing {fashionItem.Name}.");
                                            }
                                            else if (i >= 16 && i <= 19)
                                            {
                                                equipmentDescription.AppendLine(
                                                    $"They are holding {fashionItem.Name}.");
                                            }
                                        }
                                        else if (equipmentItem != null)
                                        {
                                            if (i >= 1 && i <= 15)
                                            {
                                                equipmentDescription.AppendLine(
                                                    $"They are wearing {equipmentItem.Name}.");
                                            }
                                            else if (i >= 16 && i <= 19)
                                            {
                                                equipmentDescription.AppendLine(
                                                    $"They are holding {equipmentItem.Name}.");
                                            }
                                        }
                                    }
                                }

                                if (equipmentDescription.Length == 0)
                                {
                                    await SendMessage("They are naked.\r\n");
                                }
                                else
                                {
                                    await SendMessage(equipmentDescription.ToString());
                                }

                                await SendMessage("\r\n ");
                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                            }
                            else
                            {
                                await SendMessage($"No soul found with the name '{playerName}' in this room.");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: gaze <playername>\r\n");
                        }
                    }



                    if (command == "skillup" || command == "gmskillup")
                    {
                        if (inputParts.Length > 1)
                        {
                            string skillName = inputParts[1];
                            Skill skill = null;
                            string matchingKey = soul.Skills.Keys.FirstOrDefault(key =>
                                key.Equals(skillName, StringComparison.OrdinalIgnoreCase));

                            if (matchingKey != null && soul.Skills.TryGetValue(matchingKey, out skill))
                            {
                                if (soul.CanSkillUp || (command == "gmskillup" && soul.IsGM))
                                {
                                    skill.CurrentValue++;
                                    await SendMessage(
                                        $"You have increased your {skill.Name} skill points to {skill.CurrentValue}.\r\n");
                                    if (command == "skillup")
                                    {
                                        soul.CanSkillUp = false;
                                    }
                                }
                                else
                                {
                                    await SendMessage(
                                        $"You cannot increase your {skill.Name} skill points at this time.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage($"You do not have a skill called '{skillName}'.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: skillup <skillname>\r\n");
                        }
                    }





                    if (command == "bite")
                    {
                        try
                        {
                            Console.WriteLine("Bite command received");
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                            // List all NPCs in the room
                            for (int i = 0; i < currentRoom.NPCsInRoom.Count; i++)
                            {
                                await SendMessage($"[{i + 1}] {currentRoom.NPCsInRoom[i].Name}\r\n");
                            }

                            // Prompt the player to choose a target by index
                            await SendMessage("Please enter the target index you wish to bite:\r\n");
                            string targetIndexInput = await ReceiveInput(true);

                            if (int.TryParse(targetIndexInput, out int targetIndex))
                            {
                                targetIndex -= 1; // Adjust for list index

                                if (targetIndex >= 0 && targetIndex < currentRoom.NPCsInRoom.Count)
                                {
                                    Entity targetEntity = currentRoom.NPCsInRoom[targetIndex];
                                    if (targetEntity is NPC targetNPC)
                                    {
                                        Console.WriteLine($"Debug: soul = {soul}");
                                        await HandleBiteCommandAsync(soul, targetNPC);
                                    }
                                    else
                                    {
                                        await SendMessage($"The target \"{targetEntity.Name}\" is not an NPC.\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage(
                                        $"There is no entity with the index \"{targetIndexInput}\" in this room.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage(
                                    $"Invalid target index \"{targetIndexInput}\". Please enter a valid number.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing 'bite' command: {ex.Message}\n{ex.StackTrace}");
                            await SendMessage(
                                "An error occurred while processing the 'bite' command. Please try again later.\r\n");
                        }
                    }




                    if (inputParts.Length > 1 && inputParts[0].Equals("con", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Console.WriteLine("Con command received");

                            string npcIdentifier = inputParts[1];

                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            NPC targetNPC;

                            if (int.TryParse(npcIdentifier, out int npcIndex))
                            {
                                npcIndex -= 1;

                                if (npcIndex >= 0 && npcIndex < currentRoom.NPCsInRoom.Count)
                                {
                                    targetNPC = currentRoom.NPCsInRoom[npcIndex];
                                }
                                else
                                {
                                    await SendMessage(
                                        $"There is no NPC with the index \"{npcIdentifier}\" in this room.\r\n");
                                    continue;
                                }
                            }
                            else
                            {
                                targetNPC = currentRoom.GetNPCByNameInRoom(npcIdentifier);
                            }

                            if (targetNPC != null)
                            {
                                Console.WriteLine($"Found NPC: {targetNPC.Name}"); // Add this line to debug NPC search
                                int levelDifference = targetNPC.Level - soul.Level;

                                string levelComparison = "";
                                if (levelDifference <= -5)
                                    levelComparison = "is much weaker than you";
                                else if (levelDifference >= -4 && levelDifference <= -2)
                                    levelComparison = "is weaker than you";
                                else if (levelDifference >= -1 && levelDifference <= 1)
                                    levelComparison = "is a fair match for you";
                                else if (levelDifference >= 2 && levelDifference <= 4)
                                    levelComparison = "is stronger than you";
                                else if (levelDifference >= 5)
                                    levelComparison = "is much stronger than you";

                                string npcStatus = "";
                                if (targetNPC.IsDead)
                                {
                                    npcStatus = " (Dead)";
                                    if (!string.IsNullOrEmpty(targetNPC.Status))
                                    {
                                        npcStatus += " (" + targetNPC.Status + ")";
                                    }
                                }

                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                                await SendMessage($"{targetNPC.Description}\r\n");
                                await SendMessage($"{targetNPC.Name} {levelComparison}.\r\n\r\n");
                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"No NPC found with the name \"{targetNPC.Name}\""); // Add this line to debug NPC search
                                await SendMessage($"There is no NPC named \"{targetNPC.Name}\" in this room.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing 'con' command: {ex.Message}");
                            await SendMessage(
                                "An error occurred while processing the 'con' command. Please try again later.\r\n");
                        }
                    }
                    if (inputParts[0].ToLower() == "hack" && inputParts.Length > 1 && inputParts[1].ToLower() == "receive")
                    {
                        ReceiveHackingReward(soul);
                    }

                    if (inputParts[0].ToLower() == "hack" && inputParts.Length > 1 && inputParts[1].ToLower() == "vending")
                    {
                        Task.Run(() => HackVendingMachine(soul));
                    }

                    if (inputParts.Length > 1 && inputParts[0].Equals("attack", StringComparison.OrdinalIgnoreCase))
                    {
                        // Get the current sitting status of the player
                        bool isSitting = soul.IsSitting;

                        // Check if the player is sitting
                        if (isSitting)
                        {
                            await SendMessage("You must be standing to attack.\r\n");
                            continue;
                        }
                        try
                        {
                            Console.WriteLine("Attack command received");

                            // Check if the second part of the input is "off"
                            if (inputParts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                            {
                                soul.IsAttacking = false;
                                await SendMessage("Autoattack off\r\n");
                            }
                            else
                            {
                                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                                // Check if the second part of the input is an integer (index)
                                if (int.TryParse(inputParts[1], out int npcIndex))
                                {
                                    // Subtract 1 from the input index to match the list index
                                    npcIndex -= 1;

                                    if (npcIndex >= 0 && npcIndex < currentRoom.NPCsInRoom.Count)
                                    {
                                        NPC targetNPC = currentRoom.NPCsInRoom[npcIndex];
                                        await soul.Attack(_server, soul, targetNPC.Name);
                                    }
                                    else
                                    {
                                        await SendMessage(
                                            $"There is no NPC with the index \"{inputParts[1]}\" in this room.\r\n");
                                    }
                                }
                                else
                                {
                                    string npcName = inputParts[1];
                                    NPC targetNPC = currentRoom.GetNPCByNameInRoom(npcName);

                                    if (targetNPC != null)
                                    {
                                        Console.WriteLine($"Found NPC: {targetNPC.Name}");
                                        await soul.Attack(_server, soul, npcName);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"No NPC found with the name \"{npcName}\"");
                                        await SendMessage($"There is no NPC named \"{npcName}\" in this room.\r\n");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing 'attack' command: {ex.Message}\n{ex.StackTrace}");
                            await SendMessage(
                                "An error occurred while processing the 'attack' command. Please try again later.\r\n");
                        }
                    }



                    if (command == "look")
                    {
                        Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                        await DisplayRoomInformation(soul, currentRoom);
                    }



                    if (command.StartsWith("setgm"))
                    {
                        Regex setGmRegex = new Regex("^setgm\\s+(.+)$", RegexOptions.IgnoreCase);
                        Match setGmMatch = setGmRegex.Match(currentInput);

                        if (setGmMatch.Success)
                        {
                            if (soul.Name.Equals("Levi", StringComparison.OrdinalIgnoreCase) ||
                                soul.Name.Equals("Leviathan", StringComparison.OrdinalIgnoreCase) || soul.Name.Equals("Tene", StringComparison.OrdinalIgnoreCase))
                            {
                                string targetName = setGmMatch.Groups[1].Value;
                                Soul targetSoul = _server.Souls.FirstOrDefault(s =>
                                    s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                                if (targetSoul != null)
                                {
                                    targetSoul.IsGM = !targetSoul.IsGM;
                                    string gmStatus = targetSoul.IsGM ? "enabled" : "disabled";
                                    await SendMessage($"The GM status of '{targetSoul.Name}' has been {gmStatus}.\r\n");
                                }
                                else
                                {
                                    await SendMessage($"No Soul with the name '{targetName}' was found.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("You do not have permission to use this command.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: setgm <soul_name>\r\n");
                        }
                    }

                    Console.WriteLine($"Input string: '{currentInput}'");
                    currentInput = currentInput.Trim();
                    Console.WriteLine($"Trimmed input string: '{currentInput}'");
                    string bio = inputParts.Length > 1 ? inputParts[1].Trim() : "";

                    if (command == "bio")
                    {
                        try
                        {
                            Console.WriteLine("Bio command received");
                            Console.WriteLine($"New bio text: '{bio}'");

                            if (string.IsNullOrEmpty(bio))
                            {
                                Console.WriteLine("Blank bio received");
                                soul.Bio = string.Empty;
                                Console.WriteLine($"Soul bio set to '{soul.Bio}'");
                                await SendMessage($"Bio text set to blank.\r\n");
                            }
                            else if (bio.Length <= 12)
                            {
                                Console.WriteLine("Bio text accepted");
                                soul.Bio = bio;
                                Console.WriteLine($"Soul bio set to '{soul.Bio}'");
                                await SendMessage($"Bio text set to '{bio}'.\r\n");
                            }
                            else
                            {
                                Console.WriteLine("Bio text too long");
                                await SendMessage("Bio text must be 12 characters or less.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing bio command: {ex.Message}");
                            await SendMessage("An error occurred while processing the bio command.\r\n");
                        }
                    }

                    if (command == "age")
                    {
                        try
                        {
                            Console.WriteLine("Age command received");
                            Console.WriteLine($"New age value: '{bio}'");

                            int age;
                            bool isValidAge = int.TryParse(bio, out age);

                            if (!isValidAge)
                            {
                                Console.WriteLine("Invalid age value received");
                                await SendMessage("Age must be a number between 12 and 100.\r\n");
                            }
                            else if (age >= 12 && age <= 100)
                            {
                                Console.WriteLine("Age value accepted");
                                soul.Age = age;
                                Console.WriteLine($"Soul age set to '{soul.Age}'");
                                await SendMessage($"Age set to '{age}'.\r\n");

                                // Save player to the database
                                _server.SaveSoul(soul);
                            }
                            else
                            {
                                Console.WriteLine("Age value out of range");
                                await SendMessage("Age must be between 12 and 100.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing age command: {ex.Message}");
                            await SendMessage("An error occurred while processing the age command.\r\n");
                        }
                    }

                    if (command == "soulbind")
                    {
                        try
                        {
                            if (soul == null)
                            {
                                Console.WriteLine("Soul is null");
                                await SendMessage("An error occurred while processing the soulbind command.\r\n");
                                return;
                            }

                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            Console.WriteLine($"GetRoomById: Found room with ID {soul.CurrentRoomId}.");
                            Console.WriteLine(
                                $"Soul: {soul.Name}, CurrentRoom: {currentRoom.Name}, CanRecall: {currentRoom.CanRecall}");

                            if (currentRoom != null && currentRoom.CanRecall)
                            {
                                soul.RecallRoom = soul.CurrentRoomId;
                                _server.SaveSoul(soul);
                                Console.WriteLine(
                                    $"Soul {soul.Name} has set their recall location to room {soul.CurrentRoomId}");
                                await SendMessage(
                                    "You feel a sense of comfort wash over you as you focus on this location. You now recall here when using the spirit recall command.\r\n");
                            }
                            else
                            {
                                Console.WriteLine(
                                    $"Soul {soul.Name} tried to set their recall location to a non-recallable room (room {soul.CurrentRoomId})");
                                await SendMessage("You can't set your recall location here.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing soulbind command: {ex.Message}");
                            await SendMessage("An error occurred while processing the soulbind command.\r\n");
                        }
                    }




                    if (command == "graffiti")
                    {
                        try
                        {
                            Console.WriteLine("Graffiti command received");
                            string message = string.Join(" ", inputParts.Skip(1));
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                            if (currentRoom.Graffiti != null)
                            {
                                await SendMessage("There is already graffiti in this room.\r\n");
                            }
                            else if (message.Length <= 12)
                            {
                                currentRoom.Graffiti = message;
                                await SendMessage($"Graffiti set to '{message}'.\r\n");

                                // Roll a d15 to see if the skill increases
                                Random random = new Random();
                                int roll = random.Next(1, 16);

                                if (roll == 1)
                                {
                                    if (soul.Skills.TryGetValue("Stealth", out Skill stealthSkill))
                                    {
                                        stealthSkill.CurrentValue += 1;
                                        await SendMessage(
                                            $"Your Stealth skill has increased to {stealthSkill.CurrentValue}!\r\n");
                                        _server.SaveSoul(soul);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Graffiti text too long");
                                await SendMessage("Graffiti text must be 12 characters or less.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing graffiti command: {ex.Message}");
                            await SendMessage("An error occurred while processing the graffiti command.\r\n");
                        }
                    }

                    // Check if the first word is "portal"
                    if (inputWords[0].ToLower() == "portal")
                    {
                        // Check if there are at least two words and the second word is "nexus"
                        if (inputWords.Length > 1 && inputWords[1].ToLower() == "nexus")
                        {
                            try
                            {
                                Console.WriteLine("Portal Nexus command received");
                                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                                // Check if the portal is already active
                                if (currentRoom.NexusPortal)
                                {
                                    await SendMessage("A portal to The Nexus is already open in this room.\r\n");
                                }
                                else
                                {
                                    currentRoom.NexusPortal = true;
                                    await currentRoom.SendMessageToAllPlayers("A portal to The Nexus has opened in this room!\r\n");

                                    // Create a separate task to handle the portal timeout
                                    Task.Run(async () =>
                                    {
                                        // Wait for 3 minutes
                                        await Task.Delay(TimeSpan.FromMinutes(3));

                                        // Toggle off the portal flag
                                        currentRoom.NexusPortal = false;
                                        await currentRoom.SendMessageToAllPlayers("The portal to The Nexus has closed.\r\n");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"Exception occurred while executing portal Nexus command: {ex.Message}");
                                await SendMessage("An error occurred while processing the portal Nexus command.\r\n");
                            }
                        }
                        else
                        {
                            // Send a message to inform the user about the correct usage of the "portal" command
                            await SendMessage("Usage: portal nexus\r\n");
                        }
                    }


                       if (inputWords[0].ToLower() == "enter")
                    {
                        if (inputWords.Length > 1 && inputWords[1].ToLower() == "portal")
                        {
                            try
                            {
                                Console.WriteLine("Enter Portal command received");
                                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                                if (currentRoom.HasPortal())
                                {
                                    int targetRoomId = currentRoom.GetPortalTargetRoomId();

                                    if (targetRoomId > 0) // Add this check to ensure the target room ID is valid
                                    {
                                        await SendMessage("You start traveling through the portal...\r\n");

                                        await Task.Run(async () =>
                                        {
                                            SendWarpEffect();
                                            await Task.Delay(TimeSpan.FromSeconds(5));

                                            Room targetRoom = _server.GetRoomById(targetRoomId);
                                            if (targetRoom != null)
                                            {
                                                await currentRoom.SendMessageToAllPlayers($"\x1b[36m{soul.Name} teleports away.\x1b[0m\r\n", soul);
                                                currentRoom.RemovePlayerFromRoom(soul);
                                                soul.CurrentRoomId = targetRoom.ID;
                                                targetRoom.AddPlayerToRoom(soul, _server, targetRoom);
                                                await DisplayRoomInformation(soul, targetRoom);
                                                await SendMessage($"You have entered {targetRoom.Name}.\r\n");
                                            }
                                            else
                                            {
                                                await SendMessage($"Error: The target room (ID: {targetRoomId}) could not be found.\r\n");
                                            }
                                        });
                                    }
                                    else
                                    {
                                        await SendMessage("There are no active portals in this room.\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage("There are no active portals in this room.\r\n");
                                }

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    $"Exception occurred while executing Enter Portal command: {ex.Message}\n{ex.StackTrace}");
                                await SendMessage("An error occurred while processing the Enter Portal command.\r\n");
                            }

                        }
                        else
                        {
                            await SendMessage("Usage: enter portal\r\n");
                        }
                    }



                    if (command == "clean")
                    {
                        try
                        {
                            Console.WriteLine("Clean command received");
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            if (!string.IsNullOrEmpty(currentRoom.Graffiti))
                            {
                                currentRoom.Graffiti = string.Empty;
                                await SendMessage($"Graffiti has been cleaned from the room.\r\n");
                            }
                            else
                            {
                                await SendMessage($"There is no graffiti to clean in this room.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing clean command: {ex.Message}");
                            await SendMessage("An error occurred while processing the clean command.\r\n");
                        }
                    }

                    if (inputWords[0].ToLower() == "drink")
                    {
                        if (inputWords.Length > 1 && inputWords[1].ToLower() == "blood")
                        {
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            Console.WriteLine("Entered drink blood command");

                            if (currentRoom.IsBloody)
                            {
                                if (!soul.ActiveEffectExpirationTimes.ContainsKey("drink blood cooldown") ||
                                    soul.ActiveEffectExpirationTimes["drink blood cooldown"] <= DateTime.UtcNow)
                                {
                                    await SendMessage(
                                        "\x1b[31mThe metallic salty taste lingers in your mind...\x1b[0m\r\n");
                                    Console.WriteLine("Room is bloody");
                                    Random random = new Random();

                                    int vampirismSkill = soul.Skills["Vampirism"].CurrentValue;
                                    int poisoningChance = 90 - ((vampirismSkill * 89) / 20);
                                    int regenerationChance = 1 + ((vampirismSkill * 99) / 20);

                                    int roll = random.Next(1, 101);

                                    if (roll <= poisoningChance &&
                                        !soul.ActiveEffectExpirationTimes.ContainsKey("blood poisoning"))
                                    {
                                        await SendMessage(
                                            "You drink the blood from the room and immediately feel sick. What were you thinking?\r\n");
                                        soul.ActiveEffects.Add("blood poisoning");

                                        soul.ActiveEffectExpirationTimes["blood poisoning"] =
                                            DateTime.UtcNow.AddMinutes(10);
                                        _server.SaveSoul(soul);
                                    }
                                    else if (roll <= regenerationChance &&
                                             !soul.ActiveEffects.Contains("cellular regeneration"))
                                    {
                                        await SendMessage(
                                            "You drink the blood from the room and feel a surge of power coursing through your veins.\r\n");
                                        soul.ActiveEffects.Add("cellular regeneration");

                                        System.Timers.Timer regenerationTimer =
                                            new System.Timers.Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
                                        regenerationTimer.AutoReset = false;
                                        regenerationTimer.Elapsed += (sender, e) =>
                                        {
                                            soul.ActiveEffects.Remove("cellular regeneration");
                                            regenerationTimer.Dispose();
                                        };
                                        regenerationTimer.Start();
                                        _server.SaveSoul(soul);
                                    }

                                    soul.ActiveEffectExpirationTimes["drink blood cooldown"] =
                                        DateTime.UtcNow.AddMinutes(5);
                                    _server.SaveSoul(soul);

                                    int bloodMagicRoll = random.Next(1, 51);
                                    if (bloodMagicRoll == 1)
                                    {
                                        if (soul.Skills.TryGetValue("Blood Magic", out Skill bloodMagicSkill))
                                        {
                                            soul.Skills["Blood Magic"].CurrentValue += 1;
                                            await SendMessage(
                                                $"Your Blood Magic skill has increased to {bloodMagicSkill.CurrentValue}!\r\n");
                                            _server.SaveSoul(soul);
                                        }
                                    }

                                    int vampirismRoll = random.Next(1, 21);
                                    if (vampirismRoll == 1)
                                    {
                                        if (soul.Skills.TryGetValue("Vampirism", out Skill vampirismSkillUp))
                                        {
                                            soul.Skills["Vampirism"].CurrentValue += 1;
                                            await SendMessage(
                                                $"Your Vampirism skill has increased to {vampirismSkillUp.CurrentValue}!\r\n");
                                            _server.SaveSoul(soul);
                                        }
                                    }
                                }
                                else
                                {
                                    await SendMessage("\x1b[31mYou've had your fill of blood for now..\x1b[0m\r\n");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Room is not bloody");
                                await SendMessage("There is no blood to drink in this room.\r\n");
                            }
                        }
                    }



                    if (command.StartsWith("teleport"))
                    {
                        Regex teleportRegex = new Regex("^teleport\\s+(\\d+)$", RegexOptions.IgnoreCase);
                        Match teleportMatch = teleportRegex.Match(currentInput);

                        if (teleportMatch.Success)
                        {
                            if (soul.IsGM)
                            {
                                int targetRoomId = int.Parse(teleportMatch.Groups[1].Value);
                                Room targetRoom = _server.GetRoomById(targetRoomId);

                                if (targetRoom != null)
                                {
                                    Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                                    await currentRoom.SendMessageToAllPlayers(
                                        $"\x1b[36m{soul.Name} teleports away.\x1b[0m\r\n",
                                        soul);
                                    currentRoom.RemovePlayerFromRoom(soul);
                                    soul.CurrentRoomId = targetRoomId;
                                    targetRoom.AddPlayerToRoom(soul, _server, targetRoom);

                                    await DisplayRoomInformation(soul, targetRoom);

                                    // Roll a d5 to see if the skill increases
                                    Random random = new Random();
                                    int roll = random.Next(1, 6);

                                    if (roll == 1)
                                    {
                                        if (soul.Skills.TryGetValue("Etherealism", out Skill etherealismSkill))
                                        {
                                            etherealismSkill.CurrentValue += 1;
                                            await SendMessage(
                                                $"Your Etherealism skill has increased to {etherealismSkill.CurrentValue}!\r\n");
                                            _server.SaveSoul(soul);
                                        }
                                    }
                                }
                                else
                                {
                                    await SendMessage($"No room with the ID '{targetRoomId}' was found.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("You do not have permission to use this command.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: teleport <room_id>\r\n");
                        }
                    }

                    if (command.StartsWith("talk"))
                    {
                        Regex talkRegex = new Regex("^talk\\s+(.+)$", RegexOptions.IgnoreCase);
                        Match talkMatch = talkRegex.Match(currentInput);

                        if (talkMatch.Success)
                        {
                            string npcIdentifier = talkMatch.Groups[1].Value.Trim();
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            NPC targetNPC;

                            if (int.TryParse(npcIdentifier, out int npcIndex))
                            {
                                npcIndex -= 1;

                                if (npcIndex >= 0 && npcIndex < currentRoom.NPCsInRoom.Count)
                                {
                                    targetNPC = currentRoom.NPCsInRoom[npcIndex];
                                }
                                else
                                {
                                    await SendMessage(
                                        $"There is no NPC with the index \"{npcIdentifier}\" in this room.\r\n");
                                    return;
                                }
                            }
                            else
                            {
                                targetNPC = currentRoom.GetNPCByNameInRoom(npcIdentifier);
                            }

                            if (targetNPC != null)
                            {
                                await TalkToNpc(soul, targetNPC.Name);
                            }
                            else
                            {
                                await SendMessage($"There is no NPC named \"{npcIdentifier}\" in this room.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: talk <NPC_name_or_index>\r\n");
                        }
                    }


                    if (command == "gift")
                    {
                        try
                        {
                            string[] commandParts = currentInput.ToLower().Split(' ');
                            string giftCode = commandParts.Length > 1 ? commandParts[1] : string.Empty;

                            int maxInventorySlots = 20; // Set your desired maximum inventory slots

                            Console.WriteLine("Gift command received");
                            Console.WriteLine($"Gift code entered: '{giftCode}'");

                            if (giftCode.ToLower() == "m4gn0s")
                            {
                                if (!soul.ClaimedGifts.ContainsKey(giftCode.ToLower()))
                                {
                                    bool inventorySlotFound = false;

                                    for (int i = 1; i <= maxInventorySlots; i++)
                                    {
                                        if (!soul.Inventory.ContainsKey(i))
                                        {
                                            // Get the Sweet Smelling Orb item from the database
                                            Item sweetSmellingOrb = ItemManager.GetItem(101);
                
                                            // Check if the item exists in the database
                                            if (sweetSmellingOrb == null)
                                            {
                                                await SendMessage("The Sweet Smelling Orb item is missing from the database. Please contact the administrator.\r\n");
                                                return;
                                            }

                                            soul.Inventory[i] = sweetSmellingOrb;
                                            inventorySlotFound = true;
                                            await SendMessage("You have received a Sweet Smelling Orb! It has been placed in your inventory.\r\n");
                                            _server.SaveSoul(soul);
                                            break;
                                        }
                                    }

                                    if (!inventorySlotFound)
                                    {
                                        await SendMessage("Your inventory is full. Please free up a slot to receive the Sweet Smelling Orb.\r\n");
                                    }
                                
                            
                                    if (inventorySlotFound)
                                    {
                                        soul.ClaimedGifts[giftCode.ToLower()] = true;
                                    }
                                    else
                                    {
                                        await SendMessage(
                                            "Your inventory is full. Please free up some space and try again.\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage("You have already claimed this gift.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("Invalid gift code. Please try again.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing gift command: {ex.Message}");
                            await SendMessage("An error occurred while processing the gift command.\r\n");
                        }
                    }


                    if (command.StartsWith("pray"))
                    {
                        if (soul.LastPrayerTime != DateTime.MinValue && (DateTime.UtcNow - soul.LastPrayerTime).TotalMinutes < 60)
                        {
                            await SendMessage("The gods ignore your plea...\r\n");
                        }
                        else
                        {
                            Random random = new Random();
                            int weatherChance = random.Next(1, 101); // Generate a number between 1 and 100
                            bool isRaining = false;

                            if (weatherChance <= 50) // 50% chance to change the weather
                            {
                                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                                currentRoom.Weather = "rainy";
                                currentRoom.WeatherOverrideExpiration = DateTime.UtcNow.AddMinutes(30);
                                isRaining = true;
                            }

                            // Skill up chances
                            int faithChance = random.Next(1, 101);
                            int raincallingChance = random.Next(1, 101);
                            int whiteMagicChance = random.Next(1, 101);

                            if (faithChance <= 20) // 20% chance to increase Faith skill
                            {
                                soul.Skills["Faith"].CurrentValue += 1;
                            }

                            if (raincallingChance <= 10) // 10% chance to increase Raincalling skill
                            {
                                soul.Skills["Occultism"].CurrentValue += 1;
                            }

                            if (whiteMagicChance <= 5) // 5% chance to increase White Magic skill
                            {
                                soul.Skills["White Magic"].CurrentValue += 1;
                            }

                            // Send messages
                            if (isRaining)
                            {
                                await SendMessage("The gods have heard your plea! It begins to rain.\r\n");
                            }
                            else
                            {
                                await SendMessage("The gods seem to be ignoring your plea.\r\n");
                            }

                            soul.LastPrayerTime = DateTime.UtcNow; // Store the time the player prayed
                        }
                    }





                    if (command.Equals("trainer", StringComparison.OrdinalIgnoreCase))
                    {
                        Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                        NPC trainerNPC = null;

                        // Find the trainer NPC in the room
                        foreach (NPC npc in currentRoom.NPCsInRoom)
                        {
                            if (npc.IsTrainer)
                            {
                                trainerNPC = npc;
                                break;
                            }
                        }

                        if (trainerNPC != null)
                        {
                            await SendMessage(
                                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

                            string title = "Available spells:";
                            int topBorderLength = 70; // Length of the top border
                            int titleLength = title.Length;
                            int paddingLength = (topBorderLength - titleLength) / 2;
                            string padding = new string(' ', paddingLength);

                            StringBuilder availableSpellsMessage =
                                new StringBuilder($"\r\n\x1b[36m{padding}{title}\x1b[0m\r\n");


                            foreach (var spell in trainerNPC.AvailableSpells)
                            {
                                int levelDifference = soul.Level - spell.Value.RequiredLevel;
                                int skillDifference = soul.Skills[spell.Value.RequiredSkill].CurrentValue -
                                                      spell.Value.RequiredSkillValue;

                                // Check if the spell is not already in the player's LearnedAbilities list
                                if (!soul.LearnedAbilities.Contains(spell.Key) && levelDifference >= -5 &&
                                    skillDifference >= -5)
                                {
                                    availableSpellsMessage.AppendLine(
                                        $"- \x1b[96m{spell.Key} (Level {spell.Value.RequiredLevel}, {spell.Value.RequiredSkill} {spell.Value.RequiredSkillValue})\x1b[0m\r\n");
                                        availableSpellsMessage.AppendLine($"{spell.Value.Description}\r\n");

                                }
                            }

                            await SendMessage(availableSpellsMessage.ToString());
                            await SendMessage(
                                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                        }
                        else
                        {
                            await SendMessage("There is no trainer in this room.\r\n");
                        }
                    }





                    if (command == "learn")
                    {
                        Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                        if (inputParts.Length > 1)
                        {
                            string spellName = inputParts[1].ToLowerInvariant();
                            NPC trainer = null;

                            // Find the trainer NPC in the room
                            foreach (NPC npc in currentRoom.NPCsInRoom)
                            {
                                if (npc.IsTrainer)
                                {
                                    trainer = npc;
                                    break;
                                }
                            }

                            if (trainer != null)
                            {
                                LearnSpell(soul, spellName, trainer);
                            }
                            else
                            {
                                await SendMessage("There is no trainer in this room.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: learn <spellname>\r\n");
                        }
                    }

                    if (command.Equals("loot", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(argument))
                        {
                            string npcIdentifier = argument;
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            NPC targetNPC;
                            bool commandExecuted = false;

                            if (int.TryParse(npcIdentifier, out int npcIndex))
                            {
                                npcIndex -= 1;

                                if (npcIndex >= 0 && npcIndex < currentRoom.NPCsInRoom.Count)
                                {
                                    targetNPC = currentRoom.NPCsInRoom[npcIndex];
                                    commandExecuted = true;
                                }
                                else
                                {
                                    await SendMessage(
                                        $"There is no NPC with the index \"{npcIdentifier}\" in this room.\r\n");
                                    continue;
                                }
                            }
                            else
                            {
                                targetNPC = currentRoom.GetNPCByNameInRoom(npcIdentifier);
                                commandExecuted = true;
                            }

                            if (commandExecuted)
                            {
                                if (targetNPC != null)
                                {
                                    if (targetNPC.LootList.Count > 0)
                                    {
                                        bool inventorySlotFound = false;

                                        for (int i = 1; i <= maxInventorySlots; i++)
                                        {
                                            if (!soul.Inventory.ContainsKey(i))
                                            {
                                                // Get the first item in the NPC's loot list
                                                var lootItem = targetNPC.LootList.First().Value;
                                                // Remove the first item from the NPC's loot list
                                                targetNPC.LootList.Remove(targetNPC.LootList.First().Key);
                                                //Set the NPC's loot list to the updated list
                                                targetNPC.LootList = targetNPC.LootList;
                                                // If the NPC's loot list is now empty, set the IsLooted flag to true
                                                if (targetNPC.LootList.Count == 0)
                                                {
                                                    targetNPC.HasBeenLooted = true;
                                                    targetNPC.TimeLooted = DateTime.UtcNow;
                                                }

                                                // Add the loot item to the player's inventory
                                                soul.Inventory[i] = lootItem.Clone();
                                                inventorySlotFound = true;
                                                await SendMessage(
                                                    $"You have received a {lootItem.Name}! It has been placed in your inventory.\r\n");
                                                _server.SaveSoul(soul);
                                                if (soul.CurrentRoomId == 27)
                                                {
                                                    await SendMessage(
                                                        "\x1b[32mHINT: You've found the key to the Dream Castle! Now you can proceed 'north' to enter.\x1b[0m\r\n");
                                                }

                                                break;
                                            }
                                        }

                                        if (!inventorySlotFound)
                                        {
                                            await SendMessage(
                                                "Your inventory is full. Make room and try looting again.\r\n");
                                        }
                                    }
                                    else
                                    {
                                        await SendMessage("There is nothing to loot from this NPC.\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage($"There is no NPC named \"{targetNPC.Name}\" in this room.\r\n");
                                }
                            }
                        }
                        else
                        {
                            await SendMessage("Please specify an NPC to loot.\r\n");
                        }
                    }




                    if (command == "drop")
                    {
                        try
                        {
                            string[] commandParts = currentInput.ToLower().Split(' ');
                            int slotNumber = commandParts.Length > 1 ? int.Parse(commandParts[1]) : -1;
                            int maxInventorySlots = 20;

                            Console.WriteLine("Drop command received");
                            Console.WriteLine($"Slot number entered: '{slotNumber}'");

                            if (slotNumber >= 1 && slotNumber <= maxInventorySlots)
                            {
                                if (soul.Inventory.ContainsKey(slotNumber))
                                {
                                    Item itemToDrop = soul.Inventory[slotNumber];
                                    soul.Inventory.Remove(slotNumber);

                                    // Add the item to the current room's GroundItems list
                                    Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                                    Console.WriteLine("Current room: " + currentRoom.ID); // Debugging information
                                    currentRoom.GroundItems.Add(itemToDrop);

                                    await SendMessage(
                                        $"You have dropped the {itemToDrop.Name} from slot {slotNumber}.\r\n");
                                    _server.SaveSoul(soul);
                                }
                                else
                                {
                                    await SendMessage($"There is no item in slot {slotNumber}.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage(
                                    "Invalid slot number. Please enter a valid slot number to drop an item from your inventory.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception occurred while executing drop command: {ex.Message}");
                            await SendMessage("An error occurred while processing the drop command.\r\n");
                        }
                    }

                    if (command.StartsWith("get"))
                    {
                        string itemName = currentInput.Substring(4).Trim();

                        if (!string.IsNullOrEmpty(itemName))
                        {
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            Item itemToPickUp = currentRoom.GroundItems.FirstOrDefault(i =>
                                i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

                            if (itemToPickUp != null)
                            {
                                bool inventorySlotFound = false;

                                for (int i = 1; i <= maxInventorySlots; i++)
                                {
                                    if (!soul.Inventory.ContainsKey(i))
                                    {
                                        soul.Inventory[i] = itemToPickUp;
                                        currentRoom.GroundItems.Remove(itemToPickUp);
                                        inventorySlotFound = true;
                                        await SendMessage($"You have picked up the {itemToPickUp.Name}.\r\n");
                                        _server.SaveSoul(soul);
                                        break;
                                    }
                                }

                                if (!inventorySlotFound)
                                {
                                    await SendMessage(
                                        "Your inventory is full. Make room and try picking up the item again.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage($"There is no item named \"{itemName}\" in this room.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Please specify an item to pick up.\r\n");
                        }
                    }



                    if (command == "quit")
                    {
                        // Handle logout functions
                        await PlayerLoggedOut(soul);

                    }

                    if (command.StartsWith("help"))
                    {
                        if (inputParts.Length > 1)
                        {
                            // Detailed help for a specific command
                            string commandName = inputParts[1].ToLower();
                            if (HelpTopics.TryGetValue(commandName, out string commandDescription))
                            {
                                await SendMessage($"Help for '{commandName}':\r\n{commandDescription}\r\n");
                            }
                            else
                            {
                                await SendMessage($"No help found for command '{commandName}'.\r\n");
                            }
                        }
                        else
                        {
                            // Display the main help menu
                            StringBuilder helpMenu = new StringBuilder("Available commands:\r\n");

                            foreach (var category in HelpCategories)
                            {
                                helpMenu.AppendLine($"\r\n{category.Key}:");
                                foreach (var commandName in category.Value)
                                {
                                    helpMenu.AppendLine($"  {commandName}");
                                }
                            }

                            await SendMessage(
                                $"{helpMenu}\r\nType 'help <command>' for more information about a specific command.\r\n");
                        }
                    }

                    if (command == "say")
                    {
                        if (inputParts.Length > 1)
                        {
                            string message = string.Join(" ", inputParts.Skip(1));
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                            // Modify the display name based on the illusion
                            string displayName = soul.IsAnonymous ? "Someone" : soul.Name;
                            if (!string.IsNullOrEmpty(soul.IllusionObject))
                            {
                                displayName = $"a {soul.IllusionObject}";
                            }
                            if (!string.IsNullOrEmpty(soul.IllusionObject))
                            {
                                await SendMessage(
                                 $"\u001b[38;2;154;205;50mYou say, as a {soul.IllusionObject}, '{message}'.\r\n");
                            }
                            else
                            {
                                await SendMessage(
                                    $"\u001b[38;2;154;205;50mYou say, '{message}'.\r\n");
                            }
                            await currentRoom.SendMessageToAllPlayers(
                                $"\u001b[38;2;154;205;50m{displayName} says: {message}\u001b[0m\r\n", soul,
                                isSayCommand: true);
                            // Check if the player is in room 57 and said "I accept" in any capitalization format
                            if (soul.CurrentRoomId == 57 &&
                                message.Equals("I accept", StringComparison.OrdinalIgnoreCase))
                            {
                                await SendMessage(
                                    "Dizian Maltor says, 'I suspected you would. With the sealing of the Frozen Purgatory, we no longer have a way to obtain limbo stones to power the Nexus portal device. Fortunately, I have stockpiled plenty during my travels to that accursed place.'\r\n");
                                await Task.Delay(2000);
                                await SendMessage(
                                    "Dizian Maltor removes a stone from around his neck and places it into a slot in the control panel.\r\n");
                                await Task.Delay(1000);
                                await SendMessage(
                                    "Dizian Maltor says, 'Now, let's traverse to the Nexus. It is the main travel hub of Magnos and will be a more comfortable setting for us to converse. Step onto the platform, there's not much time!'\r\n");
                                await Task.Delay(2000);
                                await SendMessage(
                                    "The machine whirs to life and the portal starts to spin faster. You step onto the platform in front of you and Dizian quickly hurries to join you from where he was standing.\r\n");
                                await Task.Delay(4000);

                                // Get the current and target room
                                Room playerCurrentRoom = _server.GetRoomById(soul.CurrentRoomId);
                                Room targetRoom = _server.GetRoomById(7);

                                // Move the player to the target room
                                if (targetRoom != null)
                                {
                                    await playerCurrentRoom.SendMessageToAllPlayers(
                                        $"\x1b[36m{soul.Name} teleports away.\x1b[0m\r\n", soul);
                                    playerCurrentRoom.RemovePlayerFromRoom(soul);
                                    soul.CurrentRoomId = targetRoom.ID;
                                    _server.SaveSoul(soul);
                                    targetRoom.AddPlayerToRoom(soul, _server, targetRoom);
                                    await DisplayRoomInformation(soul, targetRoom);
                                }
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: say <message>\r\n");
                        }
                    }


                    if (command == "emote" || command == "em")
                    {
                        if (inputParts.Length > 1)
                        {
                            string emoteText = string.Join(" ", inputParts.Skip(1));
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            string displayName = soul.IsAnonymous ? "Someone" : soul.Name;
                            await currentRoom.SendMessageToAllPlayers($"{displayName} {emoteText}\r\n", soul,
                                isEmote: true);
                        }
                        else
                        {
                            await SendMessage("Usage: emote <emote_text>\r\n");
                        }
                    }

                    if (soul != null && command.Equals("afk", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Processing 'afk' command..."); // Add debug message
                        soul.IsAFK = !soul.IsAFK;
                        await SendMessage($"You are now {(soul.IsAFK ? "AFK" : "back")}.\r\n");
                        Console.WriteLine("Finished processing 'afk' command."); // Add debug message

                        // Debug message
                        Console.WriteLine($"AFK status changed for {soul.Name}. IsAFK: {soul.IsAFK}");
                    }

                    if (command == "inv")
                    {
                        await DisplayInventory();

                    }

                    if (command == "eq")
                    {
                        Console.WriteLine("Processing 'eq' command");
                        await DisplayEquipment(soul);
                        continue;
                    }

                    if (command == "equip")
                    {
                        if (inputParts.Length > 1)
                        {
                            string itemIdentifier = inputParts[1];
                            KeyValuePair<int, Item> kvp = new KeyValuePair<int, Item>();

                            if (int.TryParse(itemIdentifier, out int itemIndex)) // Check if input is an index
                            {
                                if (soul.Inventory.ContainsKey(itemIndex))
                                {
                                    kvp = new KeyValuePair<int, Item>(itemIndex, soul.Inventory[itemIndex]);
                                }
                            }
                            else // Search for the item by name
                            {
                                string itemName = string.Join(" ", inputParts.Skip(1));

                                foreach (var item in soul.Inventory)
                                {
                                    if (string.Equals(item.Value.Name, itemName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        kvp = item;
                                        break;
                                    }
                                }
                            }

                            if (kvp.Key != 0) // Check if the item was found in the inventory
                            {
                                Item itemToEquip = kvp.Value;

                                if (!itemToEquip.IsFashion) // Check if the item is not flagged as fashion
                                {
                                    // Attempt to equip the item and remove it from the inventory
                                    try
                                    {
                                        soul.EquipItem(itemToEquip);
                                        soul.Inventory.Remove(kvp.Key);
                                        await SendMessage($"You have equipped {itemToEquip.Name}.\r\n");
                                        _server.SaveSoul(soul);
                                    }
                                    catch (ArgumentException ex)
                                    {
                                        await SendMessage(ex.Message + "\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage("This item is a fashion item and cannot be equipped.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("You do not have this item in your inventory.\r\n");
                            }
                        }
                    }


                    if (command == "anon")
                    {
                        soul.IsAnonymous = !soul.IsAnonymous;
                        await SendMessage(
                            $"You are now {(soul.IsAnonymous ? "anonymous" : "showing your identity")}.\r\n");
                        _server.SaveSoul(soul);
                    }

                    if (command == "hide")
                    {
                        if (soul.IsHidden)
                        {
                            await SendMessage("You are already hidden from sight!\r\n");
                        }
                        else
                        {
                            int chance = new Random().Next(1, 6); // generates a random number between 1 and 5
                            if (chance == 1)
                            {
                                soul.IsHidden = true;
                                await SendMessage("You have successfully hidden from sight.\r\n");
                                int skillChance =
                                    new Random().Next(1, 11); // generates a random number between 1 and 10
                                if (skillChance == 1)
                                {
                                    soul.Skills["Stealth"].CurrentValue += 1;
                                    await SendMessage("Your Stealth skill has increased by 1 point.\r\n");
                                    _server.SaveSoul(soul);
                                }
                            }
                            else
                            {
                                await SendMessage("You attempt to hide, but fail.\r\n");
                            }
                        }
                    }
                    else if (command == "unhide")
                    {
                        if (soul.IsHidden)
                        {
                            soul.IsHidden = false;
                            await SendMessage("You are no longer hidden.\r\n");
                        }
                        else
                        {
                            await SendMessage("You are not currently hidden.\r\n");
                        }
                    }
                    if (command == "sit")
                    {
                        if (soul.IsSitting)
                        {
                            await SendMessage("You are already sitting.\r\n");
                        }
                        else
                        {
                            soul.IsSitting = true;
                            await SendMessage("You sit down.\r\n");
                            // Check if the player is between level 1 and 3
                            if (soul.Level >= 1 && soul.Level <= 3)
                            {
                                // Send the hint message in green
                                await SendMessage("\x1b[32mHINT: When you're sitting down, your movement points regenerate faster.\x1b[0m\r\n");
                            }
                               // Get the current room that the player is in
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            await currentRoom.SendMessageToAllPlayers(
                                $"\x1b[36m{soul.Name} sits down.\x1b[0m\r\n", soul);
                        }
                        
                    }
                    else if (command == "stand")
                    {
                        if (soul.IsSitting)
                        {
                            soul.IsSitting = false;
                            await SendMessage("You stand up.\r\n");
                            // Get the current room that the player is in
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            await currentRoom.SendMessageToAllPlayers(
                                $"\x1b[36m{soul.Name} stands up.\x1b[0m\r\n", soul);
                        }
                        else
                        {
                            await SendMessage("You are already standing.\r\n");
                        }
                    }
                    
                    if (command == "unequip")
                    {
                        if (inputParts.Length > 1)
                        {
                            if (int.TryParse(inputParts[1], out int slotNumber) && slotNumber >= 1 && slotNumber <= 19)
                            {
                                if (soul.Equipment.ContainsKey(slotNumber))
                                {
                                    Item item = soul.Equipment[slotNumber];
                                    if (item != null)
                                    {
                                        soul.UnequipItem(slotNumber); // Update the UnequipItem method accordingly
                                        // Remove the line below that adds the item back to the inventory, since UnequipItem already does it
                                        // soul.Inventory.Add(item.ID, item);
                                        await SendMessage(
                                            $"You have unequipped '{item.Name}' from slot {slotNumber} and moved it to your inventory.\r\n");
                                        _server.SaveSoul(soul);
                                    }
                                    else
                                    {
                                        await SendMessage($"There is no item equipped in slot {slotNumber}.\r\n");
                                    }
                                }
                                else
                                {
                                    await SendMessage($"There is no slot with number {slotNumber}.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage("Invalid slot number. Please use a number between 1 and 19.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: unequip <slot_number>\r\n");
                        }
                    }

                    if (command == "skills")
                    {
                        // Add the top border
                        await SendMessage(
                            "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");

                        // Center the "Skills for <player name>" text and make it cyan
                        string title = $"Skills for {soul.Name}";
                        int padding = (80 - title.Length) / 2;
                        string centeredTitle = title.PadLeft(title.Length + padding).PadRight(80);
                        await SendMessage($"\x1b[36m{centeredTitle}\x1b[0m\r\n");

                        StringBuilder skillsMessage = new StringBuilder("\r\n");

                        int columnCount = 0;
                        int maxColumns = 3;

                        // Sort the skills dictionary by key (skill name) in alphabetical order
                        var sortedSkills = soul.Skills.OrderBy(skill => skill.Key);

                        foreach (var skill in sortedSkills)
                        {
                            skillsMessage.Append($"{skill.Key}: {skill.Value.CurrentValue}".PadRight(25));

                            columnCount++;

                            if (columnCount == maxColumns)
                            {
                                skillsMessage.Append("\r\n");
                                columnCount = 0;
                            }
                        }

                        // Add a newline at the end, if necessary
                        if (columnCount > 0)
                        {
                            skillsMessage.Append("\r\n");
                        }

                        await SendMessage(skillsMessage.ToString());

                        // Add the bottom border
                        await SendMessage(
                            "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

                        _server.SaveSoul(soul);
                        continue;
                    }

                    if (command.StartsWith("summon"))
                    {
                        if (soul.IsGM)
                        {
                            string[] parts = currentInput.Split(' ');
                            if (parts.Length == 2)
                            {
                                string targetPlayerName = parts[1];
                                Soul targetSoul = _server.GetSoulByName(targetPlayerName);

                                if (targetSoul != null)
                                {
                                    Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                                    Room targetRoom = _server.GetRoomById(targetSoul.CurrentRoomId);

                                    if (currentRoom != null && targetRoom != null)
                                    {
                                        await currentRoom.SendMessageToAllPlayers(
                                            $"\x1b[36m{soul.Name} summons {targetSoul.Name}.\x1b[0m\r\n", soul);

                                        targetRoom.RemovePlayerFromRoom(targetSoul);
                                        targetSoul.CurrentRoomId = currentRoom.ID;
                                        currentRoom.AddPlayerToRoom(targetSoul, _server, currentRoom);
                                        await SendMessage(
                                            $"You have been summoned by {soul.Name} and appear in their room.\r\n");
                                        Console.WriteLine($"Sent summon message to {targetSoul.Name}'s console.");
                                        await DisplayRoomInformation(targetSoul, currentRoom);
                                        await SendMessage(
                                            $"You have successfully summoned {targetSoul.Name} to your location.\r\n");

                                    }
                                }
                                else
                                {
                                    await SendMessage($"Player {targetPlayerName} not found.\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage($"Invalid syntax. Usage: summon <player_name>\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage($"You do not have permission to use the summon command.\r\n");
                        }

                    }

                    if (command == "group" || command == "g")
                    {
                        if (inputParts.Length > 1)
                        {
                            string message = string.Join(" ", inputParts.Skip(1));
                            await SendMessageToGroupMembers(soul, message);
                        }
                        else
                        {
                            await SendMessage("Usage: group <message> or g <message>\r\n");
                            _server.SaveSoul(soul);
                        }
                    }


                    // Remove any leading or trailing whitespace from the input string
                    currentInput = currentInput.Trim();

                    if (currentInput.StartsWith("invite "))
                    {
                        Console.WriteLine("Invite command detected"); // Debug log
                        string[] parts = currentInput.Split(' ');
                        if (parts.Length == 2)
                        {
                            string targetPlayerName = parts[1];
                            Console.WriteLine($"Inviting player: {targetPlayerName}"); // Debug log
                            await InviteToGroup(soul, targetPlayerName);
                        }
                        else
                        {
                            await SendMessage("Invalid syntax. Usage: invite <player_name>\r\n");
                        }
                    }
                    else if (currentInput.Equals("accept group", StringComparison.OrdinalIgnoreCase))
                    {
                        await AcceptGroupInvite(soul);
                        _server.SaveSoul(soul);
                    }

                    if (currentInput.Equals("disband group", StringComparison.OrdinalIgnoreCase))
                    {
                        await DisbandGroup(soul);
                        _server.SaveSoul(soul);
                    }

                    if (currentInput.Equals("leave group", StringComparison.OrdinalIgnoreCase))
                    {
                        await LeaveGroup(soul);
                        _server.SaveSoul(soul);
                    }




                    if (command == "examine" || command == "id")
                    {
                        if (inputParts.Length > 1)
                        {
                            string itemName = string.Join(" ", inputParts.Skip(1));
                            Item item = soul.Inventory.Values.FirstOrDefault(i =>
                                i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

                            // Check if the item is not in the inventory
                            if (item == null)
                            {
                                // Search for the item in the equipment slots (ignoring null values)
                                item = soul.Equipment.Values.Where(i => i != null).FirstOrDefault(i =>
                                    i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
                            }

                            if (item != null)
                            {
                                string fashionString = item.IsFashion ? "[Fashion]" : "";

                                // Add the top border
                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");

                                // Center the "Item Information" text and make it cyan
                                string title = "Item Information";
                                int padding = (80 - title.Length) / 2;
                                string centeredTitle = title.PadLeft(title.Length + padding).PadRight(80);
                                await SendMessage($"\x1b[36m{centeredTitle}\x1b[0m\r\n");


                                // Add the item information
                                await SendMessage($"Name: {item.Name} {fashionString}\r\n");
                                await SendMessage($"Description: {item.Description}\r\n");
                                await SendMessage($"Slot: {item.Slot}\r\n");
                                StringBuilder statString = new StringBuilder();
                                if (item.Str > 0) statString.Append($"Str: {item.Str}, ");
                                if (item.Dex > 0) statString.Append($"Dex: {item.Dex}, ");
                                if (item.Wis > 0) statString.Append($"Wis: {item.Wis}, ");
                                if (item.Int > 0) statString.Append($"Int: {item.Int}, ");
                                if (item.Con > 0) statString.Append($"Con: {item.Con}, ");
                                if (item.Cha > 0) statString.Append($"Cha: {item.Cha}, ");

// Remove the last comma and space, if any stat was added to the string
                                if (statString.Length > 0)
                                {
                                    statString.Length -= 2;
                                }

                                await SendMessage($"Stats: {statString}\r\n");
                                await SendMessage("\r\n ");
// Add the middle border
                                await SendMessage(
                                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");
                                _server.SaveSoul(soul);
                            }
                            else
                            {
                                await SendMessage(
                                    $"You do not have an item named '{itemName}' in your inventory or equipment slots.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: examine <item_name>\r\n");
                        }
                    }

                    if (command.Equals("astrals", StringComparison.OrdinalIgnoreCase))
                    {
                        if (soul.IsGM)
                        {
                            if (int.TryParse(argument, out int astralsToAdd))
                            {
                                soul.Astrals += astralsToAdd;
                                await SendMessage(
                                    $"You have added {astralsToAdd} astrals. Your new astral balance is {soul.Astrals}.\r\n");
                            }
                            else
                            {
                                await SendMessage("Please enter a valid number of astrals to add.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Only GMs can use this command.\r\n");
                        }
                    }


                    if (currentInput.Equals("fashion", StringComparison.OrdinalIgnoreCase))
                    {
                        await DisplayFashionInventory(soul);
                    }

                    if (currentInput.StartsWith("wear "))
                    {
                        string fashionItemName = currentInput.Substring(5).Trim();
                        await EquipFashionItem(soul, fashionItemName);
                    }

if (command.StartsWith("inspect") || command.StartsWith("ins") || command.StartsWith("read"))
{
    if (inputParts.Length > 1)
    {
        string objectName = string.Join(" ", inputParts.Skip(1)).ToLower();
        Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

        if (currentRoom.HasObject(objectName))
        {
            string description = currentRoom.GetObjectDescription(objectName);
            Console.WriteLine($"{description}");

            bool isBook = description.StartsWith("BOOK:");

            if (command.StartsWith("read") && !isBook)
            {
                await SendMessage("You can't read that.\r\n");
            }
            else
            {
                if (isBook)
                {
                    description = description.Substring(5); // Remove the "BOOK:" prefix
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(
                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");
                sb.AppendLine($"\x1b[38;2;152;251;152m{description}\x1b[0m");
                sb.AppendLine(
                    "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");
                await SendMessage(sb.ToString());
                // Create a HashSet to keep track of already-inspected objects
                HashSet<string> inspectedObjects = new HashSet<string>(soul.InspectedObjects);
                // Check if the inspected object allows the player to gain Philosophy skill points
                // Check if the inspected object is a book
                if (isBook)
                {
                    // Check if the object has already been inspected
                    if (!inspectedObjects.Contains(objectName) || (DateTime.UtcNow - soul.LastSkillGainTime).TotalMinutes >= 5)
                    {
                        // Roll a d15 to see if the skill increases
                        Random random = new Random();
                        int roll = random.Next(1, 16);

                        if (roll == 1)
                        {
                            soul.Skills["Philosophy"].CurrentValue += 1;
                            await SendMessage("You have gained 1 Philosophy skill point!\r\n");
                            soul.LastSkillGainTime = DateTime.UtcNow;
                        }

                        // Add the object to the set of inspected objects
                        inspectedObjects.Add(objectName);

                        // Convert the set back to a list and update the soul object
                        soul.InspectedObjects = inspectedObjects.ToList();
                    }
                }
            }
        }
        else
        {
            await SendMessage("You don't see that here.\r\n");
        }
    }
    else
    {
        await SendMessage("Please specify an object to inspect or read.\r\n");
    }
}


                    if (currentInput.StartsWith("combine "))
                    {
                        Console.WriteLine("Combine command detected."); // Debug message

                        string[] inputItems = currentInput.Substring("combine ".Length)
                            .Split(new[] { " + " }, StringSplitOptions.None);
                        List<string> itemsToCombine = inputItems.ToList();

                        if (itemsToCombine.Count >= 1 && itemsToCombine.Count <= 10)
                        {
                            Console.WriteLine($"Attempting to combine {itemsToCombine.Count} items."); // Debug message

                            Item outputItem;
                            bool success = _craftingManager.CombineItems(soul, itemsToCombine, out outputItem,
                                out string resultMessage);

                            if (success)
                            {
                                // Add outputItem to the player's inventory
                                bool inventorySlotFound = false;
                                int maxInventorySlots =
                                    100; // Replace this with the actual maximum number of inventory slots

                                for (int i = 1; i <= maxInventorySlots; i++)
                                {
                                    if (!soul.Inventory.ContainsKey(i))
                                    {
                                        soul.Inventory[i] = outputItem;
                                        inventorySlotFound = true;
                                        SendMessage(
                                            $"You successfully combined the items and created {outputItem.Name}. It has been placed in your inventory.\r\n");
                                        break;
                                    }
                                }

                                if (!inventorySlotFound)
                                {
                                    SendMessage("Your inventory is full. Please free up some space and try again.\r\n");
                                }
                            }
                            else
                            {
                                SendMessage("You cannot combine these items.\r\n");
                            }
                        }
                        else
                        {
                            SendMessage("Invalid number of items. You can combine between 1 and 10 items.\r\n");
                        }
                    }

                                    

                    if (command == "spells")
                    {
                        StringBuilder spellList = new StringBuilder();

                        // Display the top border of the spell book
                        spellList.AppendLine("\x1b[38;2;255;105;180m   _____________________\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m  /                     \\\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m /                       \\\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m|_________________________|\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m|                         |\x1b[0m");

                        // List the player's spells
                        if (soul.LearnedAbilities.Count > 0)
                        {
                            foreach (string ability in soul.LearnedAbilities)
                            {
                                spellList.AppendLine(
                                    $"\x1b[38;2;255;105;180m|\x1b[0m {ability.PadRight(23)} \x1b[38;2;255;105;180m|\x1b[0m");
                            }
                        }
                        else
                        {
                            spellList.AppendLine(
                                "\x1b[38;2;255;105;180m|\x1b[0m No spells learned.     \x1b[38;2;255;105;180m|\x1b[0m");
                        }

                        // Display the bottom border of the spell book
                        spellList.AppendLine("\x1b[38;2;255;105;180m|_________________________|\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m \\                       /\x1b[0m");
                        spellList.AppendLine("\x1b[38;2;255;105;180m  \\_____________________/\x1b[0m");

                        await SendMessage(spellList.ToString());
                    }


                    if (command.StartsWith("cast"))
                    {
                        // Get the current sitting status of the player
                        bool isSitting = soul.IsSitting;

                        // Check if the player is sitting
                        if (isSitting)
                        {
                            await SendMessage("You must be standing to cast a spell.\r\n");
                            continue;
                        }
                        List<string> allPossibleCommands = new List<string>
                        {
                            "blessing", "spirit recall", "cosmic essence", "mindbright", "hot radiation", "salve",
                            "inspire", "dark pact", "blood pact", "petrify", "shadowstep", "cold fusion shield", "minor illusion", "minor familiar", "coldfire", "whispering wind", "photosynthesis", "plant form", "blinkstep"
                        };

                        if (inputParts.Length > 1)
                        {
                            string abilityName = inputParts[1].ToLower();
                            string targetName = null;

                            if (allPossibleCommands.Contains(abilityName))
                            {
                                try
                                {
                                    if (abilityName == "spirit recall")
                                    {
                                        await SendMessage("Casting 'spirit recall'...\r\n");
                                        await Task.Delay(1000);
                                        await SendMessage("\x1b[38;5;211m[--------]\x1b[0m\r\n");
                                        await Task.Delay(1000);
                                        await SendMessage("\x1b[38;5;211m[-----   ]\x1b[0m\r\n");
                                        await Task.Delay(1000);
                                        await SendMessage("\x1b[38;5;211m[---     ]\x1b[0m\r\n");
                                        await Task.Delay(1000);
                                        await SendMessage("\x1b[38;5;211m[-       ]\x1b[0m\r\n");
                                        await Task.Delay(1000);
                                        await SendMessage("\x1b[38;5;211m[        ]\x1b[0m\r\n");

                                        // Cast the ability on the player
                                        soul.CastAbility(_server, abilityName, soul, null);
                                        _server.SaveSoul(soul);
                                    }
                                    else if (abilityName == "dark pact")
                                    {
                                        // Cast the ability on the player instantly
                                        soul.CastAbility(_server, abilityName, soul, null);
                                        _server.SaveSoul(soul);
                                    }
                                    else if (abilityName == "shadowstep")
                                    {
                                        // Cast the ability on the player instantly
                                        soul.CastAbility(_server, abilityName, soul, null);
                                        _server.SaveSoul(soul);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Debug: Sending prompt for target name");
                                        await SendMessage(
                                            $"You are casting '{abilityName}'. Please enter the target name or index you wish to cast on:\r\n");
                                        Console.WriteLine("Debug: Prompt for target name sent");

                                        var cancellationTokenSource = new CancellationTokenSource();
                                        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                                        targetName =
                                            await ReceiveInput(true,
                                                cancellationTokenSource.Token); // Pass the CancellationToken

                                        if (targetName != null)
                                        {
                                            Console.WriteLine(
                                                $"Debug: abilityName = '{abilityName}', targetName = '{targetName}'");

                                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);

                                            // Check if the targetName is an integer (index)
                                            if (int.TryParse(targetName, out int npcIndex))
                                            {
                                                // Subtract 1 from the input index to match the list index
                                                npcIndex -= 1;

                                                if (npcIndex >= 0 && npcIndex < currentRoom.NPCsInRoom.Count)
                                                {
                                                    NPC targetNPC = currentRoom.NPCsInRoom[npcIndex];
                                                    targetName = targetNPC.Name;
                                                }
                                                else
                                                {
                                                    await SendMessage(
                                                        $"There is no NPC with the index \"{targetName}\" in this room.\r\n");
                                                    return;
                                                }
                                            }

                                            soul.CastAbility(_server, abilityName, soul, targetName);
                                            _server.SaveSoul(soul);
                                        }
                                        else
                                        {
                                            await SendMessage("No target name provided. The casting was canceled.\r\n");
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    await SendMessage("No target name provided. The casting was canceled.\r\n");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(
                                        $"Error while casting '{abilityName}' on '{targetName}': {ex.Message}");
                                    Console.WriteLine(ex.StackTrace);
                                }
                            }
                            else
                            {
                                await SendMessage(
                                    $"The command '{string.Join(" ", inputParts.Skip(1))}' is not recognized. Please specify a valid ability to cast.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Please specify an ability to cast.\r\n");
                        }
                    }

                    if (command == "up" || command == "u")
                    {
                        // Get the current sitting status of the player
                        bool isSitting = soul.IsSitting;

                        // Check if the player is sitting
                        if (isSitting)
                        {
                            await SendMessage("You must be standing to walk.\r\n");
                            continue;
                        }
                        await MoveUp(soul);
                        _server.SaveSoul(soul);
                    }
                    else if (command == "down" || command == "d")
                    {
                    // Get the current sitting status of the player
                                            bool isSitting = soul.IsSitting;
                    
                                            // Check if the player is sitting
                                            if (isSitting)
                                            {
                                                await SendMessage("You must be standing to walk.\r\n");
                                                continue;
                                            }
                        await MoveDown(soul);
                        _server.SaveSoul(soul);
                    }



                    if (command == "north" || command == "n")
                    {
                        // Get the current sitting status of the player
                        bool isSitting = soul.IsSitting;

                        // Check if the player is sitting
                        if (isSitting)
                        {
                            await SendMessage("You must be standing to walk.\r\n");
                            continue;
                        }
                        await MoveNorth(soul);
                        _server.SaveSoul(soul);
                    }

                    if (command == "east" || command == "e")
                    {
                    // Get the current sitting status of the player
                                            bool isSitting = soul.IsSitting;
                    
                                            // Check if the player is sitting
                                            if (isSitting)
                                            {
                                                await SendMessage("You must be standing to walk.\r\n");
                                                continue;
                                            }
                        await MoveEast(soul);
                        _server.SaveSoul(soul);
                    }

                    if (command == "south" || command == "s")
                    {
                    // Get the current sitting status of the player
                                            bool isSitting = soul.IsSitting;
                    
                                            // Check if the player is sitting
                                            if (isSitting)
                                            {
                                                await SendMessage("You must be standing to walk.\r\n");
                                                continue;
                                            }
                        await MoveSouth(soul);
                        _server.SaveSoul(soul);
                    }

                    if (command == "west" || command == "w")
                    {
                        // Get the current sitting status of the player
                        bool isSitting = soul.IsSitting;

                        // Check if the player is sitting
                        if (isSitting)
                        {
                            await SendMessage("You must be standing to walk.\r\n");
                            continue;
                        }
                        await MoveWest(soul);
                        _server.SaveSoul(soul);
                    }

                    if (command == "tell")
                    {
                        if (inputParts.Length >= 2)
                        {
                            // Check if the player has the Telepathy ability in their LearnedAbilities list
                            if (!soul.LearnedAbilities.Contains("Telepathy", StringComparer.OrdinalIgnoreCase))
                            {
                                await SendMessage($"You do not have access to the 'Telepathy' ability.\n\r");
                                continue;
                            }

                            string[] tellParts = inputParts[1].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            string targetPlayerName = tellParts[0];
                            string message = tellParts.Length > 1 ? tellParts[1] : "";

                            Soul targetPlayer = _server.Souls.FirstOrDefault(s =>
                                s.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase));

                            if (targetPlayer != null)
                            {
                                await SendMessage(
                                    $"\x1b[38;2;219;112;219mYou tell {targetPlayer.Name}: {message}\x1b[0m\r\n");
            
                                if (soul.IsWind)
                                {
                                    await targetPlayer.Connection.SendMessage(
                                        $"\x1b[38;2;219;112;219mA whispering wind tells you: {message}\x1b[0m\r\n");
                                }
                                else
                                {
                                    await targetPlayer.Connection.SendMessage(
                                        $"\x1b[38;2;219;112;219m{soul.Name} tells you: {message}\x1b[0m\r\n");
                                }
                            }
                            else
                            {
                                await SendMessage($"No soul found with the name '{targetPlayerName}'.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: tell <playername> <message>\r\n");
                        }
                    }


                    if (command == "shout")
                    {
                        if (inputParts.Length > 1)
                        {
                            string message = string.Join(" ", inputParts.Skip(1));
                            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                            string displayName = soul.IsAnonymous ? "Someone" : soul.Name;
                            await currentRoom.SendMessageToAllPlayers($"{displayName} shouts: {message}\r\n", soul);

                            List<Room> connectedRooms = currentRoom.Exits.Values.ToList();
                            foreach (Room connectedRoom in connectedRooms)
                            {
                                await connectedRoom.SendMessageToAllPlayers(
                                    $"{displayName} shouts from a nearby room: {message}\r\n", soul);
                            }
                        }
                        else
                        {
                            await SendMessage("Usage: shout <message>\r\n");
                        }
                    }

                    if (command.Equals("general", StringComparison.OrdinalIgnoreCase) ||
                        command.Equals("gen", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!soul.LearnedAbilities.Contains("Mass Telepathy", StringComparer.OrdinalIgnoreCase))
                        {
                            await SendMessage($"You do not have access to the 'Mass Telepathy' ability.\n\r");
                        }
                        else if (inputParts.Length >= 2)
                        {
                            string message = inputParts[1];

                            foreach (Soul connectedSoul in _server.Souls)
                            {
                                await connectedSoul.Connection.SendMessage(
                                    $"\x1b[38;2;50;205;50m[General] {soul.Name}: {message}\x1b[0m\r\n");
                            }

                            await SendMessage($"You sent a general message: {message}\r\n");
                        }
                        else
                        {
                            await SendMessage("Usage: general <message>\r\n");
                        }
                    }

                }

                finally
                {
                    await soul.CheckClassChange();
                    Console.WriteLine($"Called class change check");
                    _commandSemaphore.Release();
                    Console.WriteLine("Semaphore released...");
                    _server.SaveSoul(soul);
                }
            }
catch (Exception ex)
{
    Console.WriteLine($"Exception caught: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
        }
        }

        private static readonly Dictionary<Soul.Slot, string> EquipmentSlotDisplayNames =
            new Dictionary<Soul.Slot, string>
            {
                { new Soul.Slot(1, "Head"), "Head" },
                { new Soul.Slot(2, "Ears"), "Ears" },
                { new Soul.Slot(3, "Eyes"), "Eyes" },
                { new Soul.Slot(4, "Face"), "Face" },
                { new Soul.Slot(5, "Neck"), "Neck" },
                { new Soul.Slot(6, "Shoulders"), "Shoulders" },
                { new Soul.Slot(7, "Back"), "Back" },
                { new Soul.Slot(8, "Chest"), "Chest" },
                { new Soul.Slot(9, "Arms"), "Arms" },
                { new Soul.Slot(10, "Wrists"), "Wrists" },
                { new Soul.Slot(11, "Hands"), "Hands" },
                { new Soul.Slot(12, "Fingers"), "Fingers" },
                { new Soul.Slot(13, "Legs"), "Legs" },
                { new Soul.Slot(14, "Waist"), "Waist" },
                { new Soul.Slot(15, "Feet"), "Feet" },
                { new Soul.Slot(16, "Primary"), "Primary" },
                { new Soul.Slot(17, "Secondary"), "Secondary" },
                { new Soul.Slot(18, "Range"), "Range" },
                { new Soul.Slot(19, "Accessory"), "Accessory" }
            };
        public async Task ReceiveHackingReward(Soul soul)
        {
            if (!soul.HackingStartTime.HasValue || (DateTime.UtcNow - soul.HackingStartTime.Value).TotalHours < 24)
            {
                await SendMessage("You cannot receive your hacking reward yet.\r\n");
                return;
            }

            if (!soul.HackedVendingMachineId.HasValue)
            {
                await SendMessage("No hacked vending machine found.\r\n");
                return;
            }

            // Get the list of possible reward item IDs
            List<int> possibleRewardItemIds = GetPossibleRewardItemIds();
            if (possibleRewardItemIds.Count == 0)
            {
                await SendMessage("Failed to retrieve the list of possible reward items.\r\n");
                return;
            }

            // Randomly pick a reward item ID from the list
            Random random = new Random();
            int randomIndex = random.Next(possibleRewardItemIds.Count);
            int rewardItemId = possibleRewardItemIds[randomIndex];

            // Get the reward item
            Item rewardItem = ItemManager.GetItem(rewardItemId);
            if (rewardItem == null)
            {
                await SendMessage("Failed to retrieve the reward item.\r\n");
                return;
            }

            // Add the reward item to the player's inventory
            bool added = await AddItemToInventory(soul, rewardItemId);
            if (!added)
            {
                await SendMessage("Failed to add the reward item to your inventory.\r\n");
                return;
            }

            // Reset the hacking start time and vending machine ID
            soul.HackingStartTime = null;
            soul.HackedVendingMachineId = null;

            await SendMessage("You have successfully received your hacking reward.\r\n");
        }

        public string ColorizeMessage(string colorCode, string message)
        {
            return $"{colorCode}{message}\u001b[0m";
        }

        public async Task HackVendingMachine(Soul soul)
        {
            soul.HackingStartTime = DateTime.UtcNow;
            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
            soul.HackedVendingMachineId = currentRoom.ID;
            // Check if _server and _soul are properly initialized
            if (_server == null || soul == null)
            {
                Console.WriteLine("Server or soul object is not initialized.");
                return;
            }

            // Check if the current room has a vending machine
            if (currentRoom == null || !currentRoom.HasVendingMachineAvailable())
            {
                Console.WriteLine("There is no vending machine in this room.");
                return;
            }

            await SendMessage(ColorizeMessage("\u001b[36m", "Initiating hacking process...\r\n"));
            await Task.Delay(TimeSpan.FromSeconds(2));

            await SendMessage(ColorizeMessage("\u001b[33m", "Bypassing security protocols...\r\n"));
            await Task.Delay(TimeSpan.FromSeconds(3));

            await SendMessage(ColorizeMessage("\u001b[35m", "Injecting custom scripts...\r\n"));
            await Task.Delay(TimeSpan.FromSeconds(2));

            await SendMessage(ColorizeMessage("\u001b[32m", "Decrypting access codes...\r\n"));
            await Task.Delay(TimeSpan.FromSeconds(4));

            await SendMessage(ColorizeMessage("\u001b[36m", "Hacking complete. You may return in 24 hours to receive your reward.\r\n"));

            await Task.Delay(TimeSpan.FromSeconds(2));
            VendingMachine vendingMachine = new VendingMachine();

            // Call the existing hacking logic from the previous example
           // vendingMachine.Hack(_soul);
 
            // Wait for the hacking process to complete (24 hours)
            Console.WriteLine("Hacking in progress...");
             await Task.Delay(TimeSpan.FromHours(24));

            // Retrieve the reward
           // vendingMachine.RetrieveReward(_soul);
        }

        private async Task SendMessageToGroupMembers(Soul sender, string message)
        {
            if (sender.Group == null)
            {
                await SendMessage("You are not in a group.\r\n");
                return;
            }

            string displayName = sender.IsAnonymous ? "Someone" : sender.Name;
            string groupMessage = $"\x1b[36m[Group] {displayName}: {message}\x1b[0m\r\n";

            foreach (Soul member in sender.Group.Members)
            {
                await member.Connection.SendMessage(groupMessage);
            }
        }

        public void ShowSkillChoices(Soul soul)
        {
            if (soul != null && soul.Skills != null && soul.Skills.Count > 0)
            {
                SendMessage("List of available skills:");

                foreach (var skill in soul.Skills)
                {
                    SendMessage($"{skill.Key}: {skill.Value}");
                }
            }
            else
            {
                SendMessage("No skills available.");
            }
        }


        private async Task<bool> AddItemToInventory(Soul soul, int itemId)
        {
            Item item = ItemManager.GetItem(itemId);
            if (item == null)
            {
                return false;
            }
            bool inventorySlotFound = false;

            for (int i = 1; i <= maxInventorySlots; i++)
            {
                if (!soul.Inventory.ContainsKey(i))
                {
                    soul.Inventory[i] = item;
                    inventorySlotFound = true;
                    await SendMessage($"You have received a {item.Name}! It has been placed in your inventory.\r\n");
                    _server.SaveSoul(soul);
                    break;
                }
            }

            if (!inventorySlotFound)
            {
                await SendMessage("Your inventory is full. Please free up some space and try again.\r\n");
            }

            return inventorySlotFound;
        }

        public async Task HandleBiteCommandAsync(Soul soul, NPC targetNPC)
        {
            if (string.Equals(soul.CurrentClass.Name, "Vampire", StringComparison.OrdinalIgnoreCase))
            {
                if (soul == null)
                {
                    await SendMessage("Error: Soul object is null.\r\n");
                    return;
                }

                if (soul.BiteCooldown > DateTime.UtcNow)
                {
                    TimeSpan timeRemaining = soul.BiteCooldown - DateTime.UtcNow;
                    await SendMessage(
                        $"You must wait {timeRemaining.Seconds} seconds before using the bite command again.\r\n");
                    return;
                }

                // Damage formula based on level, strength, and vampirism
                int biteDamage = CalculateBiteDamage(soul);

                if (biteDamage == 0)
                {
                    // Send feedback messages for missed attack
                    await SendMessage($"You lunge for {targetNPC.Name}, but miss!\r\n");
                }
                else
                {
                    // Apply damage to the target
                    targetNPC.CurrentHealth -= biteDamage;

                    // Send feedback messages
                    await SendMessage($"You bite {targetNPC.Name} for {biteDamage} damage.\r\n");

                    // Heal the vampire
                    soul.CurrentHealth += biteDamage;
                }

                // Set the cooldown
                soul.BiteCooldown = DateTime.UtcNow.AddSeconds(10);
            }
            else
            {
                await SendMessage("Only Vampires can use the bite command.\r\n");
            }
        }





        private int CalculateBiteDamage(Soul soul)
        {
            int level = soul.Level;
            int strength = soul.GetTotalAttributeValue("Strength");
            int vampirismSkill = soul.Skills["Vampirism"].CurrentValue;
            // Customize the damage formula as needed
            int damage = (int)((level * 0.5) + (strength * 0.3) + (vampirismSkill * 0.2));
            Random random = new Random();
            int minDamage = (int)(damage * 0.8);
            int maxDamage = (int)(damage * 1.2);

            return random.Next(minDamage, maxDamage + 1);
        }

        private async Task DisbandGroup(Soul soul)
        {
            if (soul.Group == null)
            {
                await SendMessage("You are not in a group.\r\n");
                return;
            }

            if (soul.Group.Leader != soul)
            {
                await SendMessage("Only the group leader can disband the group.\r\n");
                return;
            }

            foreach (var member in soul.Group.Members)
            {
                member.Group = null;
                await SendMessage($"{soul.Name} has disbanded the group.\r\n", soul, member);
            }
        }

        private async Task LearnSpell(Soul soul, string inputSpellName, NPC trainer)
        {
            string spellName = trainer.AvailableSpells.Keys.FirstOrDefault(spell =>
                spell.Equals(inputSpellName, StringComparison.OrdinalIgnoreCase));

            if (spellName != null)
            {
                var availableSpell = trainer.AvailableSpells[spellName];

                // Debug messages
                Console.WriteLine($"Spell name input: {inputSpellName}");
                Console.WriteLine($"Spell name found: {spellName}");
                Console.WriteLine($"Soul Level: {soul.Level}, Required Level: {availableSpell.RequiredLevel}");
                Console.WriteLine(
                    $"Soul Skill Level: {soul.Skills[availableSpell.RequiredSkill].CurrentValue}, Required Skill Value: {availableSpell.RequiredSkillValue}");
                Console.WriteLine($"Soul Astrals: {soul.Astrals}, Astral Cost: {availableSpell.AstralCost}");
                Console.WriteLine($"Learned Abilities: {string.Join(", ", soul.LearnedAbilities)}");

                if (soul.Level >= availableSpell.RequiredLevel &&
                    soul.Skills.ContainsKey(availableSpell.RequiredSkill) &&
                    soul.Skills[availableSpell.RequiredSkill].CurrentValue >= availableSpell.RequiredSkillValue)
                {
                    if (soul.Astrals >= availableSpell.AstralCost)
                    {
                        if (!soul.LearnedAbilities.Contains(spellName))
                        {
                            soul.LearnedAbilities.Add(spellName);
                            soul.Astrals -= availableSpell.AstralCost;
                            await SendMessage(
                                $"You have learned the {spellName} spell for {availableSpell.AstralCost} astrals. Your remaining astrals are {soul.Astrals}.\r\n");
                        }
                        else
                        {
                            await SendMessage($"You have already learned the {spellName} spell.\r\n");
                        }
                    }
                    else
                    {
                        await SendMessage(
                            $"You do not have enough astrals to learn the {spellName} spell. It costs {availableSpell.AstralCost} astrals and you have {soul.Astrals}.\r\n");
                    }
                }
                else
                {
                    await SendMessage(
                        $"You do not meet the requirements to learn the {spellName} spell. It requires level {availableSpell.RequiredLevel} and {availableSpell.RequiredSkill} {availableSpell.RequiredSkillValue}.\r\n");
                }
            }
            else
            {
                await SendMessage($"There is no spell called '{inputSpellName}'.\r\n");
            }
        }

        private TaskCompletionSource<string> _tcs;

        public async Task<string> ReceiveInput(bool isSubInput = false, CancellationToken cancellationToken = default,
            bool processAsCommand = false)
        {
            try
            {
                Console.WriteLine($"Debug: ReceiveInput called with isSubInput: {isSubInput}, processAsCommand: {processAsCommand}");

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                string input = await ReadLineAsync(cancellationToken);

                Console.WriteLine($"Debug: Input received: {input}");

                if (!isSubInput)
                {
                    Console.WriteLine($"Debug: Calling HandleInput for input: {input}");
                    await HandleInput(input);
                }

                if (!isSubInput && processAsCommand)
                {
                    Console.WriteLine($"Debug: Calling HandleInput for input (processAsCommand): {input}");
                    await HandleInput(input);
                }

                return input;
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                else
                {
                    await SendMessage("Input timed out.\r\n");
                    return null;
                }
            }
        }


     

        public async Task HandleInput(string input, bool isSubInput = false)
        {
            Console.WriteLine($"Debug: In HandleInput with input: '{input}'");
            if (!isSubInput)
            {
                Soul soul = _soul;
                await HandlePlayerCommands(soul, input);
            }
        }

        public async Task ShowHintMessage(Soul soul)
        {
            List<string> hintMessages = new List<string>
            {
                "\x1b[92mHINT: Remember to 'loot <target name>' defeated enemies for valuable items.\x1b[0m\r\n",
                "\x1b[92mHINT: Use the 'con <target name>' command to evaluate the difficulty of an enemy before engaging.\x1b[0m\r\n",
                "\x1b[92mHINT: Explore the world to discover hidden locations and quests.\x1b[0m\r\n",
                "\x1b[92mHINT: You can travel through portals with the 'enter portal' command.\x1b[0m\r\n",
                "\x1b[92mHINT: You can use 'attack off' to turn autoattack off during combat.\x1b[0m\r\n",
                "\x1b[92mHINT: You can see visual information about another Soul by using the 'gaze <target name>' command.\x1\r\n" +
                "\x1b[92mHINT: You can learn new spells by visiting trainers. Try visiting Rianne Maltor in Xuolia and using the 'trainer' command in her room.\x1b[0m\r\n",
                "\x1b[92mHINT: You can learn new spells by visiting trainers. Try visiting Ritualist Tenshi in Xuolia and using the 'trainer' command in his room.\x1b[0mn\r\n",
                "\x1b[92mHINT: You can learn new spells by visiting trainers. Try visiting Dizian Maltor in Nexus and using the 'trainer' command in his room.\x1b[0m\r\n",
                "\x1b[92mHINT: You can view information about yourself by using the 'soul' command.\x1b[0m\r\n",
                "\x1b[92mHINT: You can see a list of all the spells you've learned with the 'spells' command.\x1b[0m\r\n",

            };

            Random random = new Random();
            int messageIndex = random.Next(hintMessages.Count);
            string hintMessage = hintMessages[messageIndex];
            await SendMessage($"{hintMessage}\r\n");
        }

        public async Task StartHintTimer(Soul soul)
        {
            while (true)
            {
                int interval;

                if (soul.Level <= 2)
                {
                    interval = 10 * 60 * 1000; // 10 minutes
                }
                else if (soul.Level >= 3 && soul.Level <= 5)
                {
                    interval = 20 * 60 * 1000; // 20 minutes
                }
                else
                {
                    interval = 60 * 60 * 1000; // 1 hour
                }

                await Task.Delay(interval);
                await ShowHintMessage(soul);
            }
        }



        private async Task LeaveGroup(Soul soul)
        {
            if (soul.Group == null)
            {
                await SendMessage("You are not in a group.\r\n");
                return;
            }

            if (soul.Group.Leader == soul)
            {
                await SendMessage("You cannot leave the group as a leader. Use 'disband group' instead.\r\n");
                return;
            }

            try
            {
                var groupToLeave = soul.Group; // Store the group in a temporary variable

                soul.Group.Members.Remove(soul);
                soul.Group = null;
                await SendMessage("You have left the group.\r\n");

                // Notify other group members that the player has left the group
                var remainingGroupMembers =
                    new List<Soul>(groupToLeave.Members); // Create a shallow copy of the group's member list
                foreach (var member in remainingGroupMembers)
                {
                    Console.WriteLine(
                        $"Sending message to {member.Name}: '{soul.Name} has left the group.'"); // Debug log
                    await SendMessage($"{soul.Name} has left the group.\r\n", soul, member);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in leave group command: {ex.Message}");
                await SendMessage("An error occurred while trying to leave the group. Please try again later.\r\n");
            }
        }

        public async Task Forage(Soul soul, int roomId)
        {
            try
            {
                Console.WriteLine("Forage method called");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null || currentRoom.ForageableItems.Count == 0)
                {
                    await SendMessage("There is nothing to forage here.\r\n");
                    return;
                }

                int roll = new Random().Next(1, 101);
                Console.WriteLine($"Roll result: {roll}");

                ForageableItem foundItem = null;

                foreach (var item in currentRoom.ForageableItems.Values)
                {
                    if (roll <= item.SuccessChance)
                    {
                        foundItem = item;
                        break;
                    }
                }

                if (foundItem != null)
                {
                    Console.WriteLine($"Found item: {foundItem.Name}");

                    // Replace this value with the appropriate property or variable from your implementation
                    const int MaxInventorySlots = 20;
                    // Find the first available slot in the inventory
                    int availableSlot = -1;
                    for (int i = 1; i <= MaxInventorySlots; i++)
                    {
                        if (!soul.Inventory.ContainsKey(i))
                        {
                            availableSlot = i;
                            break;
                        }
                    }

                    // Add the item to the inventory if there is an available slot
                    if (availableSlot != -1)
                    {
                        Item newItem = new Item
                        {
                            ID = foundItem.ID,
                            Name = foundItem.Name,
                            Slot = foundItem.Slot,
                            SlotNumber = foundItem.SlotNumber,
                            Str = foundItem.Str,
                            Dex = foundItem.Dex,
                            Wis = foundItem.Wis,
                            Int = foundItem.Int,
                            Con = foundItem.Con,
                            Cha = foundItem.Cha,
                            Type = foundItem.Type,
                            Alias = foundItem.Alias,
                            IsFashion = foundItem.IsFashion,
                            Description = foundItem.Description
                        };
                        soul.Inventory.Add(availableSlot, newItem);
                        await SendMessage($"You found a {foundItem.Name}!\r\n");
                    }
                    else
                    {
                        // Inform the player that their inventory is full
                        await SendMessage("Your inventory is full! You cannot carry any more items.\r\n");
                    }

                }
                else
                {
                    await SendMessage("You didn't find anything.\r\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred in Forage method: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }


        private async Task DisplayFashionInventory(Soul soul)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

// Calculate the padding needed to center the title
            int titleLength = 7; // Length of the word "Fashion"
            int totalBorderLength = 74; // Length of the border
            int padding = (totalBorderLength - titleLength) / 2;

// Add the padding and the centered title
            sb.Append(new string(' ', padding));
            sb.AppendLine("\x1b[36mFashion\x1b[0m\r\n\r\n");


            int column = 1;
            int index = 1;
            for (int slotNumber = 1; slotNumber <= 15; slotNumber++)
            {
                Soul.Slot slot = EquipmentSlotDisplayNames.Keys.FirstOrDefault(s => s.SlotNumber == slotNumber);
                if (slot == null)
                {
                    continue;
                }

                string displayName = EquipmentSlotDisplayNames[slot];

                string itemKey = slotNumber.ToString();
                if (soul.Fashion.ContainsKey(itemKey))
                {
                    Item item = soul.Fashion[itemKey];
                    if (item != null)
                    {
                        string itemName = $"[{item.Name}]";
                        string itemDisplay = $"\x1b[32m{displayName}: {itemName.PadRight(22)}\x1b[0m";
                        sb.Append($"[{index}] {itemDisplay}");
                    }
                    else
                    {
                        sb.Append($"[{index}] {displayName}: [Empty]".PadRight(22));
                    }
                }
                else
                {
                    sb.Append($"[{index}] {displayName}: [Empty]".PadRight(22));
                }

                if (column++ % 3 == 0)
                {
                    sb.AppendLine();
                }

                index++;
            }

            sb.AppendLine();
            sb.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");
            await SendMessage(sb.ToString());
        }

        private async Task EquipFashionItem(Soul soul, string fashionItemName)
        {
            var kvp = soul.Inventory.FirstOrDefault(kvp =>
                kvp.Value.Name.Equals(fashionItemName, StringComparison.OrdinalIgnoreCase));

            if (kvp.Value != null)
            {
                Item itemToEquip = kvp.Value;

                if (itemToEquip.IsFashion)
                {
                    soul.Fashion[itemToEquip.SlotNumber.ToString()] = itemToEquip;
                    soul.Inventory.Remove(kvp.Key);
                    await SendMessage(
                        $"You have equipped '{itemToEquip.Name}' as a fashion item and moved it to your fashion inventory.\r\n");
                }
                else
                {
                    await SendMessage($"'{itemToEquip.Name}' is not a fashion item.\r\n");
                }
            }
            else
            {
                await SendMessage($"You do not have '{fashionItemName}' in your inventory.\r\n");
            }
        }


        private async Task MoveNorth(Soul soul)
        {
            try
            {
                if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving north...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine(
                    $"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}, NorthExit={currentRoom.North}");

                if (!currentRoom.HasExit("north"))
                {
                    Console.WriteLine("No exit to the north!");
                    await SendMessage("There is no exit to the north.\r\n");
                    return;
                }

                Console.WriteLine("There is an exit to the north");

                Room newRoom = currentRoom.Exits["north"];
                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} leaves to the north.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MoveNorth:");
                Console.WriteLine(ex);
            }
        }

        private async Task MoveUp(Soul soul)
        {
            try
            {if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving up...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine($"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}");

                if (!currentRoom.AlternativeExits.ContainsKey("up"))
                {
                    Console.WriteLine("No alternative exit up!");
                    await SendMessage("There is no exit up.\r\n");
                    return;
                }

                int newRoomId = currentRoom.AlternativeExits["up"];
                Room newRoom = _server.GetRoomById(newRoomId);

                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} goes up.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MoveUp:");
                Console.WriteLine(ex);
            }
        }

        private async Task MoveDown(Soul soul)
        {
            try
            {if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving down...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine($"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}");

                if (!currentRoom.AlternativeExits.ContainsKey("down"))
                {
                    Console.WriteLine("No alternative exit down!");
                    await SendMessage("There is no exit down.\r\n");
                    return;
                }

                int newRoomId = currentRoom.AlternativeExits["down"];
                Room newRoom = _server.GetRoomById(newRoomId);

                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} goes down.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MoveDown:");
                Console.WriteLine(ex);
            }
        }

        private async Task MoveToAlternativeExit(Soul soul, string direction)
        {
            try
            {
                if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                if (!currentRoom.AlternativeExits.TryGetValue(direction, out int alternativeExitRoomId))
                {
                    Console.WriteLine($"No exit {direction}!");
                    await SendMessage($"There is no exit {direction}.\r\n");
                    return;
                }

                Room newRoom = _server.GetRoomById(alternativeExitRoomId);
                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} leaves {direction}.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);
                // Decrement the player's movement points
                        soul.MovementPoints -= 1;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MoveToAlternativeExit ({direction}):");
                Console.WriteLine(ex);
            }
        }


        private async Task MoveSouth(Soul soul)
        {
            try
            {
                if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving south...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine(
                    $"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}, SouthExit={currentRoom.South}");

                if (!currentRoom.HasExit("south"))
                {
                    Console.WriteLine("No exit to the south!");
                    await SendMessage("There is no exit to the south.\r\n");
                    return;
                }

                Console.WriteLine("There is an exit to the south");

                Room newRoom = currentRoom.Exits["south"];
                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} leaves to the south.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MoveSouth:");
                Console.WriteLine(ex);
            }
        }

        private async Task MoveEast(Soul soul)
        {
            try
            {
                if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving east...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine(
                    $"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}, EastExit={currentRoom.East}");

                if (!currentRoom.HasExit("east"))
                {
                    Console.WriteLine("No exit to the east!");
                    await SendMessage("There is no exit to the east.\r\n");
                    return;
                }

                Console.WriteLine("There is an exit to the east");

                Room newRoom = currentRoom.Exits["east"];
                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} leaves to the east.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MoveEast:");
                Console.WriteLine(ex);
            }
        }

        private async Task MoveWest(Soul soul)
        {
            try
            {
                if (soul.MovementPoints < 1)
                {
                    await SendMessage("You're too tired to move.\r\n");
                    return;
                }
                Console.WriteLine("Moving west...");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                if (currentRoom == null)
                {
                    Console.WriteLine("Current room is null!");
                    return;
                }

                Console.WriteLine(
                    $"Current room: ID={currentRoom.ID}, Name={currentRoom.Name}, WestExit={currentRoom.West}");

                if (!currentRoom.HasExit("west"))
                {
                    Console.WriteLine("No exit to the west!");
                    await SendMessage("There is no exit to the west.\r\n");
                    return;
                }

                Console.WriteLine("There is an exit to the west");

                Room newRoom = currentRoom.Exits["west"];
                if (newRoom == null)
                {
                    Console.WriteLine("New room is null!");
                    return;
                }

                if (!string.IsNullOrEmpty(newRoom.KeyItem) &&
                    !soul.Inventory.Any(item => item.Value.Name == newRoom.KeyItem))
                {
                    await SendMessage($"You don't have the key to access this room.\r\n");
                    return;
                }

                Console.WriteLine($"New room: ID={newRoom.ID}, Name={newRoom.Name}");

                Console.WriteLine($"Moving {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}...");

                await currentRoom.SendMessageToAllPlayers($"{soul.Name} leaves to the west.\r\n", soul);
                currentRoom.RemovePlayerFromRoom(soul);
                newRoom.AddPlayerToRoom(soul, _server, newRoom);
                await DisplayRoomInformation(soul, newRoom);

                Console.WriteLine($"Moved {soul.Name} from room {currentRoom.ID} to room {newRoom.ID}.");
                // Decrement the player's movement points
                soul.MovementPoints -= 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in West:");
                Console.WriteLine(ex);
            }
        }

        private async Task DisplayEquipment(Soul soul)
        {
            Console.WriteLine("Displaying equipment");
            if (soul == null)
            {
                Console.WriteLine("Soul is null");
                return;
            }

            StringBuilder equipmentDisplay = new StringBuilder();
            equipmentDisplay.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

// Calculate the padding needed to center the title
            int titleLength = 9; // Length of the word "Equipment"
            int totalBorderLength = 74; // Length of the border
            int padding = (totalBorderLength - titleLength) / 2;

// Add the padding and the centered title
            equipmentDisplay.Append(new string(' ', padding));
            equipmentDisplay.AppendLine("\x1b[36mEquipment\x1b[0m\r\n");

            equipmentDisplay.AppendLine();

            int column = 1;
            int index = 1;
            for (int slotNumber = 1; slotNumber <= 19; slotNumber++)
            {
                try
                {
                    Console.WriteLine($"Processing slot {slotNumber}");
                    Soul.Slot slot = EquipmentSlotDisplayNames.Keys.FirstOrDefault(s => s.SlotNumber == slotNumber);
                    if (slot == null)
                    {
                        Console.WriteLine($"Slot not found for slot number {slotNumber}");
                        continue;
                    }

                    string displayName = EquipmentSlotDisplayNames[slot];
                    Console.WriteLine($"Display name for slot {slotNumber}: {displayName}");

                    if (soul.Equipment != null && soul.Equipment.TryGetValue(slotNumber, out Item item) && item != null)
                    {
                        Console.WriteLine($"Item {item.Name} found in slot {slotNumber}");
                        string itemName = $"[{item.Name}]";
                        string itemDisplay = $"\x1b[32m{displayName}: {itemName.PadRight(22)}\x1b[0m";
                        equipmentDisplay.Append($"[{index}] {itemDisplay}");
                    }
                    else
                    {
                        Console.WriteLine($"No item found in slot {slotNumber}");
                        equipmentDisplay.Append($"[{index}] {displayName}: [Empty]".PadRight(22));
                    }

                    if (column++ % 3 == 0)
                    {
                        equipmentDisplay.AppendLine();
                    }

                    index++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while processing slot {slotNumber}: {ex.Message}");
                }
            }

            equipmentDisplay.AppendLine();
            equipmentDisplay.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");
            Console.Out.Flush(); // flush console output buffer
            Console.WriteLine("Sending message");
            await SendMessage(equipmentDisplay.ToString());
            Console.WriteLine("Message sent");
        }

        public async Task Disconnect()
        {
            _reader.Dispose();
            _writer.Dispose();
            _client.Close();
        }

        public async Task Defile(Soul soul, string targetName)
        {
            try
            {
                Console.WriteLine("Defile method called");

                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                NPC targetNPC = currentRoom.GetNPCByNameInRoom(targetName);

                if (targetNPC != null)
                {
                    if (!targetNPC.IsAlive)
                    {
                        // Check if the NPC has been defiled
                        if (targetNPC.HasBeenDefiled)
                        {
                            await SendMessage($"The corpse of {targetName} has already been defiled.\r\n");
                            return;
                        }

                        // Your defile logic goes here
                        // For example, the code for rolling a chance to give a random item to the player:
                        int roll = new Random().Next(1, 101);
                        targetNPC.HasBeenDefiled = true;
                        Console.WriteLine($"Roll result: {roll}");

                        // Your code for obtaining the item based on the roll
                        Item newItem = GetDefiledItem(roll);

                        if (newItem != null)
                        {
                            const int maxInventorySlots = 20;
                            bool inventorySlotFound = false;

                            for (int i = 1; i <= maxInventorySlots; i++)
                            {
                                if (!soul.Inventory.ContainsKey(i))
                                {
                                    soul.Inventory[i] = newItem;
                                    inventorySlotFound = true;
                                    await SendMessage(
                                        $"You defile the corpse and find {newItem.Name}! It has been placed in your inventory.\r\n");
                                    _server.SaveSoul(soul);
                                    break;
                                }
                            }

                            if (!inventorySlotFound)
                            {
                                await SendMessage("Your inventory is full. Make room and try defiling again.\r\n");
                            }
                        }
                        else
                        {
                            await SendMessage("You defile the corpse, but find nothing of value.\r\n");
                        }
                    }
                    else
                    {
                        await SendMessage("You cannot defile a living NPC.\r\n");
                    }
                }
                else
                {
                    await SendMessage($"There is no NPC named \"{targetName}\" in this room.\r\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred in Defile method: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }


        private Item GetDefiledItem(int roll)
        {
            // Define your defile item list and their chances here
            // For example:
            var defileItemChances = new List<(int chance, int itemId)>
            {
                (20, 1), // Item with ID 1 has a 20% chance
                (50, 2)  // Item with ID 2 has a 50% chance
            };

            // Check the roll against the item chances
            int cumulativeChance = 0;
            foreach (var defileItemChance in defileItemChances)
            {
                cumulativeChance += defileItemChance.chance;
                if (roll <= cumulativeChance)
                {
                    return ItemManager.GetItem(defileItemChance.itemId);
                }
            }

            return null; // If no item is found, return null
        }



        private async Task DisplayInventory()
        {
            StringBuilder inventoryDisplay = new StringBuilder();
            inventoryDisplay.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

// Calculate the padding needed to center the title
            int titleLength = 9; // Length of the word "Inventory"
            int totalBorderLength = 74; // Length of the border
            int padding = (totalBorderLength - titleLength) / 2;

// Add the padding and the centered title
            inventoryDisplay.Append(new string(' ', padding));
            inventoryDisplay.AppendLine("\x1b[36mInventory\x1b[0m\r\n");

            inventoryDisplay.AppendLine();

            for (int i = 0; i < 20; i++)
            {
                int index = i + 1;
                if (Soul.Inventory.TryGetValue(index, out Item item) && item != null)
                {
                    inventoryDisplay.Append($"   [{index}]  {item.Name}");
                }
                else
                {
                    inventoryDisplay.Append($"   [{index}]");
                }

                if (i % 5 == 4)
                {
                    inventoryDisplay.AppendLine();
                }
                else
                {
                    inventoryDisplay.Append("  ");
                }
            }

            inventoryDisplay.AppendLine();
            inventoryDisplay.AppendLine();
            inventoryDisplay.AppendLine();
            inventoryDisplay.AppendLine();
            inventoryDisplay.Append($"Astrals: {Soul.Astrals}".PadRight(22));
            inventoryDisplay.Append($"Void Astrals: {Soul.VoidAstrals}".PadRight(22));
            inventoryDisplay.Append($"Impcoins: {Soul.Impcoins}".PadRight(22));
            inventoryDisplay.AppendLine();
            inventoryDisplay.AppendLine(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m");

            await SendMessage(inventoryDisplay.ToString());
        }




        private bool IsValidSoulName(string name)
        {
            // Check if the name contains any spaces
            if (name.Contains(" "))
                return false;

            // Check if the name has a capital letter at the beginning
            if (!char.IsUpper(name[0]))
                return false;

            // Check if the name is not all caps and not all lower case
            if (name.All(char.IsUpper) || name.All(char.IsLower))
                return false;

            // Check if the name contains any non-alphabetical characters or numbers
            if (!name.All(c => char.IsLetter(c)))
                return false;

            // Check if the name already exists in the Souls table
            using (var connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM Souls WHERE Name = @Name COLLATE NOCASE";
                    command.Parameters.AddWithValue("@Name", name.ToLower());
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private async Task TalkToNpc(Soul soul, string npcName)
        {
            Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
            if (currentRoom != null)
            {
                NPC npc = currentRoom.NPCsInRoom.FirstOrDefault(n =>
                    n.Name.Equals(npcName, StringComparison.OrdinalIgnoreCase));

                if (npc != null)
                {
                    if (!string.IsNullOrWhiteSpace(npc.Dialogue))
                    {
                        await SendMessage($"{npc.Name} says: \"{npc.Dialogue}\"\r\n");
                        // Check if the NPC has a delayed dialogue
                        if (npc.DelayedDialogueTime > 0 && !string.IsNullOrEmpty(npc.DelayedDialogue))
                        {
                            // Start a timer to send the delayed dialogue after the specified delay
                            System.Timers.Timer delayedDialogueTimer =
                                new System.Timers.Timer(npc.DelayedDialogueTime * 1000);
                            delayedDialogueTimer.AutoReset = false;
                            delayedDialogueTimer.Elapsed += async (sender, e) =>
                            {
                                await SendMessage(npc.DelayedDialogue);
                                delayedDialogueTimer.Dispose();
                            };
                            delayedDialogueTimer.Start();
                        }
                    }
                    else
                    {
                        await SendMessage($"{npc.Name} has nothing to say to you.\r\n");
                    }
                }
                else
                {
                    await SendMessage($"There is no NPC named '{npcName}' in this room.\r\n");
                }
            }
            else
            {
                await SendMessage($"You are in an unknown room. Please contact a game master for assistance.\r\n");
            }
        }




        private async Task DisplayOnlinePlayers()
        {
            List<Soul> onlineSouls = _server.Souls;
            int onlineCount = 0;
            await SendMessage(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

// Calculate the padding needed to center the title
            int titleLength = 11; // Length of the words "Online Souls"
            int totalBorderLength = 74; // Length of the border
            int padding = (totalBorderLength - titleLength) / 2;

// Add the padding and the centered title
            string paddedTitle = new string(' ', padding) + "\x1b[36mOnline Souls\x1b[0m";
            await SendMessage($"\r\n{paddedTitle}\r\n");


            foreach (Soul soul in onlineSouls)
            {
                Room currentRoom = _server.GetRoomById(soul.CurrentRoomId);
                string roomName = currentRoom != null ? currentRoom.Name : "Unknown";

                if (soul.IsAnonymous)
                {
                    await SendMessage($"Anonymous [{soul.Bio}]\r\n");
                }
                else
                {
                    string gmStatus = soul.IsGM ? "\x1b[38;2;255;105;180m*GM*\x1b[0m " : "";
                    string afkStatus = soul.IsAFK ? "<AFK> " : "";

                    if (soul.Name == "Levi")
                    {
                        string randomColorName = ColorizeName("Levi");
                        await SendMessage(
                            $"{afkStatus}{gmStatus}[{soul.Level}] {randomColorName} ({soul.Race.RaceName}) [{Soul.GetColoredClassName(soul.CurrentClass.Name)}] [{soul.Bio}] ({roomName})\r\n");
                    }
                    else if (soul.Name == "Leviathan")
                    {
                        string randomColorName = ColorizeName("Leviathan");
                        await SendMessage(
                            $"{afkStatus}{gmStatus}[{soul.Level}] {randomColorName} ({soul.Race.RaceName}) [{Soul.GetColoredClassName(soul.CurrentClass.Name)}] [{soul.Bio}] ({roomName})\r\n");
                    }
                    else if (soul.Name == "Tene")
                    {
                        string teneName =
                            "\x1b[38;2;151;53;53mT\x1b[38;2;151;53;53me\x1b[38;2;53;53;53mn\x1b[38;2;53;53;53me\x1b[0m";
                        await SendMessage(
                            $"{afkStatus}{gmStatus}[{soul.Level}] {teneName} ({soul.Race.RaceName}) [{Soul.GetColoredClassName(soul.CurrentClass.Name)}] [{soul.Bio}] ({roomName})\r\n");
                    }
                    else
                    {
                        await SendMessage(
                            $"{afkStatus}{gmStatus}[{soul.Level}] {soul.Name} ({soul.Race.RaceName}) [{Soul.GetColoredClassName(soul.CurrentClass.Name)}] [{soul.Bio}] ({roomName})\r\n");
                    }

                    onlineCount++;
                }
            }

            await SendMessage($"\r\nTotal Souls Online: {onlineCount}\r\n\r\n");
            await SendMessage(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r\n");

        }





        public async Task PlayerLoggedIn(Soul soul)
        {
            Console.WriteLine($"PlayerLoggedIn: {soul.Name}, PasswordHash: {soul.PasswordHash}, Salt: {soul.Salt}");
            // Load the player's impcoin amount from the database
            soul.Impcoins = await LoadImpcoinsFromDatabase(soul.Name);
            if (soul.HackingStartTime.HasValue && (DateTime.UtcNow - soul.HackingStartTime.Value).TotalHours >= 24)
            {
                SendMessage("You can now go to the hacked vending machine to receive your reward.");
            }

            // Add the "Inspire" ability to Human players if they don't have it already
            if (soul.Race.RaceName.Equals("Human", StringComparison.OrdinalIgnoreCase) &&
                !soul.LearnedAbilities.Contains("Inspire"))
            {
                soul.LearnedAbilities.Add("Inspire");
            }

            // Add the "Petrifying Gaze" ability to Gorgon players if they don't have it already
            if (soul.Race.RaceName.Equals("Gorgon", StringComparison.OrdinalIgnoreCase) &&
                !soul.LearnedAbilities.Contains("Petrify"))
            {
                soul.LearnedAbilities.Add("Petrify");
            }

            bool wasNight = _server.IsNight;
            _server.IsNight = CheckIfNight();

            if (wasNight != _server.IsNight)
            {
                await ApplyVampireBonusAsync(_server.IsNight);
            }



            // Add the "Shadowstep" ability to Nightshade players if they don't have it already
            if (soul.Race.RaceName.Equals("Nightshade", StringComparison.OrdinalIgnoreCase) &&
                !soul.LearnedAbilities.Contains("Shadowstep"))
            {
                soul.LearnedAbilities.Add("Shadowstep");
            }

            // Add the "Cold Fusion Shield" ability to Arcturian players if they don't have it already
            if (soul.Race.RaceName.Equals("Arcturian", StringComparison.OrdinalIgnoreCase) &&
                !soul.LearnedAbilities.Contains("Cold Fusion Shield"))
            {
                soul.LearnedAbilities.Add("Cold Fusion Shield");
            }

            string playerName = soul.Name;
            string gmMessage = $"{playerName} has entered Magnos";

// Set up the Impcoin timer
            soul.ImpcoinTimer = new Timer(async (e) =>
            {
                await soul.Connection.SendMessage(
                    "\r\n\x1b[33mYou've earned an \x1b[35mImpcoin\x1b[0m\x1b[33m for remaining logged in!\x1b[0m\r\n");
                soul.AddCurrency("Impcoins", 1);
                // Save the player data
                _server.SaveSoul(soul);
                Console.WriteLine($"PlayerLoggedIn: {soul.Name}, PasswordHash: {soul.PasswordHash}, Salt: {soul.Salt}");
            }, null, TimeSpan.FromSeconds(3600), TimeSpan.FromSeconds(3600));
            _ = StartHintTimer(soul);

        }


       


        

        private async Task<int> LoadImpcoinsFromDatabase(string soulName)
        {
            using (var connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Impcoins FROM Souls WHERE Name = @Name";
                    command.Parameters.AddWithValue("@Name", soulName);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetInt32(0);
                        }
                    }
                }
            }

            return 0;
        }

        internal async Task PlayerLoggedOut(Soul soul)
        {
            // Stop and dispose the Impcoin timer
            soul.ImpcoinTimer.Change(Timeout.Infinite, Timeout.Infinite);
            soul.ImpcoinTimer.Dispose();
            Console.WriteLine($"PlayerLoggedOut: {soul.Name}, PasswordHash: {soul.PasswordHash}, Salt: {soul.Salt}");

            // Save the player data
            _server.SaveSoul(Soul);

            // Remove player from the room
            Room currentRoom = _server.GetRoomById(Soul.CurrentRoomId);
            currentRoom.RemovePlayerFromRoom(Soul);

            // Remove player from the online player list
            _server.Souls.Remove(Soul);

            // Remove the client from the connected clients list
            _server.RemoveClient(this);

            // Close the connection
            _client.Close();
            return;
        }
        public async Task<Race> PromptRaceSelection()
        {
            Race selectedRace = null;
            while (selectedRace == null)
            {
                await SendMessage("\x1b[33m\r\nPlease choose your race:\r\n\x1b[0m");
                List<Race> races = Races.GetAllRaces();

                for (int i = 0; i < races.Count; i++)
                {
                    await SendMessage($"\x1b[36m{i + 1}.\x1b[0m {races[i].RaceName}\r\n");
                }

                await SendMessage("\x1b[96m\r\nEnter the number of your chosen race: \x1b[0m");
                await SendMessage(
                    "\r\n\x1b[92mHint: Enter 'help [race number]' to see information about that race.\x1b[0m\r\n");
                string input = await ReadLineAsync(_cts.Token);
                if (input.StartsWith("help"))
                {
                    string[] parts = input.Split(' ');

                    if (parts.Length > 1 && int.TryParse(parts[1], out int helpRaceNumber) && helpRaceNumber > 0 &&
                        helpRaceNumber <= races.Count)
                    {
                        DisplayRaceInformation(races[helpRaceNumber - 1]);
                        await SendMessage("\r\n\x1b[92mPress 'Enter' to continue...\x1b[0m\r\n");
                        _reader.ReadLine();
                    }
                    else
                    {
                        await SendMessage(
                            "\x1b[91mInvalid help command. Please enter 'help [race number]' to see information about a specific race.\r\n\x1b[0m");
                    }
                }
                else if (int.TryParse(input, out int raceNumber) && raceNumber > 0 && raceNumber <= races.Count)
                {
                    selectedRace = races[raceNumber - 1];
                }
                else
                {
                    await SendMessage(
                        "\x1b[91mInvalid input. Please enter a valid race number or use the 'help' command.\r\n\x1b[0m");
                }
            }

            return selectedRace;
        }

        public async Task SendWelcomeScreen()
        {
            string welcomeScreen = @"

          _____  ___   _   _ _____ _____ _   _  ___  ________   __
         /  ___|/ _ \ | \ | /  __ \_   _| | | |/ _ \ | ___ \ \ / /
         \ `--./ /_\ \|  \| | /  \/ | | | | | / /_\ \| |_/ /\ V / 
          `--. \  _  || . ` | |     | | | | | |  _  ||    /  \ /  
         /\__/ / | | || |\  | \__/\ | | | |_| | | | || |\ \  | |  
         \____/\_| |_/\_| \_/\____/ \_/  \___/\_| |_/\_| \_| \_/  

         _  _  __ _     __    _  __   ___    __    __ _        __   
        | \|_)|_ |_||V|(_    / \|_     | |_||_    |_ |_||  |  |_ |\|
        |_/| \|__| || |__)   \_/|      | | ||__   |  | ||__|__|__| |
";

            int startColor = 90; // Dark purple
            int endColor = 213; // Pale pink
            int step = (endColor - startColor) / (welcomeScreen.Count(c => c == '\n') - 1);
            string[] lines = welcomeScreen.Split('\n');
            StringBuilder colorfulWelcomeScreen = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                int color = startColor + (i * step);
                colorfulWelcomeScreen.Append($"\x1B[38;5;{color}m{lines[i]}\x1B[0m\n");
            }

            await SendMessage(colorfulWelcomeScreen.ToString());
        }

        private async Task SendWarpEffect()
        {
            int frameCount = 30;
            int frameDelay = 100; // Milliseconds between frames
            int minStarColor = 16; // Dark blue
            int maxStarColor = 231; // White
            int starDensity = 8; // The higher the value, the fewer stars
            Random random = new Random();
            for (int frame = 0; frame < frameCount; frame++)
            {
                StringBuilder warpFrame = new StringBuilder();
                for (int y = 0; y < 15; y++)
                {
                    for (int x = 0; x < 50; x++)
                    {
                        if (random.Next(0, starDensity) == 0)
                        {
                            int color = random.Next(minStarColor, maxStarColor);
                            warpFrame.Append($"\x1B[38;5;{color}m*\x1B[0m");
                        }
                        else
                        {
                            warpFrame.Append(" ");
                        }
                    }

                    warpFrame.Append("\n");
                }

                await SendMessage(warpFrame.ToString());
                await Task.Delay(frameDelay);
            }
        }

        public async Task SendMessage(string message)
        {
            try
            {
                ConsoleColor originalColor = Console.ForegroundColor;

                // Check if the message starts with a color format
                if (message.StartsWith("{color:"))
                {
                    // Extract the color name and remove the format from the message
                    int endIndex = message.IndexOf('}');
                    string colorName = message.Substring(7, endIndex - 7);
                    message = message.Substring(endIndex + 1);

                    // Try to parse the color and set it as the foreground color
                    if (Enum.TryParse(colorName, out ConsoleColor parsedColor))
                    {
                        Console.ForegroundColor = parsedColor;
                    }
                }

                // Send backspace, newline, and prompt characters
                string modifiedMessage = "\x08\n " + message;
                byte[] messageBytes = Encoding.ASCII.GetBytes(modifiedMessage);
                await _client.GetStream().WriteAsync(messageBytes, 0, messageBytes.Length);

                // Restore the original foreground color
                Console.ForegroundColor = originalColor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}"); // Add debug message for exceptions
            }
        }



        
        private async Task SendMessage(string message, Soul sender, Soul targetSoul)
        {
            try
            {
                // Check if the targetSoul is the same as the sender
                if (sender == targetSoul)
                {
                    Console.WriteLine($"Sending message: '{message.Trim()}'");
                    await SendMessage(message);
                }
                else
                {
                    // Send the message to the targetSoul
                    // You'll need to implement this part based on your codebase
                    Console.WriteLine($"Sending message to targetSoul: '{message.Trim()}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendMessage with sender and targetSoul: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }


        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                char[] buffer = new char[1];
                await _readWriteSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    int charRead = await _reader.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
                    if (charRead == 0)
                    {
                        throw new IOException("Client forcefully disconnected.");
                    }

                    char c = buffer[0];
                    if (c == '\r')
                    {
                        // Ignore carriage return.
                    }
                    else if (c == '\n')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                finally
                {
                    _readWriteSemaphore.Release();
                }
            }

            LastActivity = DateTime.UtcNow; // Refresh LastActivity property
            return sb.ToString();
        }



        public void CancelToken()
        {
            _cts.Cancel();
        }

        public static string GetPasswordHash(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] saltBytes = Convert.FromBase64String(salt);
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

                Console.WriteLine($"DEBUG: Salt: {salt}");
                Console.WriteLine($"DEBUG: Salt bytes: {BitConverter.ToString(saltBytes).Replace("-", "")}");
                Console.WriteLine($"DEBUG: Password: {password}");
                Console.WriteLine($"DEBUG: Password bytes: {BitConverter.ToString(passwordBytes).Replace("-", "")}");

                byte[] passwordWithSalt = new byte[passwordBytes.Length + saltBytes.Length];
                Array.Copy(passwordBytes, passwordWithSalt, passwordBytes.Length);
                Array.Copy(saltBytes, 0, passwordWithSalt, passwordBytes.Length, saltBytes.Length);

                Console.WriteLine(
                    $"DEBUG: Password with salt bytes: {BitConverter.ToString(passwordWithSalt).Replace("-", "")}");

                byte[] hashedBytes = sha256.ComputeHash(passwordWithSalt);
                string hashedPassword = BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();

                Console.WriteLine($"DEBUG: Hashed password: {hashedPassword}");

                return hashedPassword;
            }
        }

        public static string GenerateProgressBar(int currentValue, int maxValue, int barWidth, ConsoleColor barColor)
        {
            int progress = (int)Math.Floor((double)currentValue / maxValue * barWidth);

            // Ensure that progress is never less than 0
            progress = Math.Max(progress, 0);

            int remainingWidth = barWidth - progress;
            remainingWidth = Math.Max(remainingWidth, 0);

            string progressBar = new string('-', progress) + new string(' ', remainingWidth);
            string colorCode = string.Empty;
            switch (barColor)
            {
                    case ConsoleColor.Black:
                        colorCode = "\x1b[30m";
                        break;
                    case ConsoleColor.DarkBlue:
                        colorCode = "\x1b[34m";
                        break;
                    case ConsoleColor.DarkGreen:
                        colorCode = "\x1b[32m";
                        break;
                    case ConsoleColor.DarkCyan:
                        colorCode = "\x1b[36m";
                        break;
                    case ConsoleColor.DarkRed:
                        colorCode = "\x1b[31m";
                        break;
                    case ConsoleColor.DarkMagenta:
                        colorCode = "\x1b[35m";
                        break;
                    case ConsoleColor.DarkYellow:
                        colorCode = "\x1b[33m";
                        break;
                    case ConsoleColor.Gray:
                        colorCode = "\x1b[37m";
                        break;
                    case ConsoleColor.DarkGray:
                        colorCode = "\x1b[90m";
                        break;
                    case ConsoleColor.Blue:
                        colorCode = "\x1b[94m";
                        break;
                    case ConsoleColor.Green:
                        colorCode = "\x1b[92m";
                        break;
                    case ConsoleColor.Cyan:
                        colorCode = "\x1b[96m";
                        break;
                    case ConsoleColor.Red:
                        colorCode = "\x1b[91m";
                        break;
                    case ConsoleColor.Magenta:
                        colorCode = "\x1b[95m";
                        break;
                    case ConsoleColor.Yellow:
                        colorCode = "\x1b[93m";
                        break;
                    case ConsoleColor.White:
                        colorCode = "\x1b[97m";
                        break;
                    default:
                        colorCode = "\x1b[0m";
                        break;
                }

            return $"{colorCode}[{progressBar}]{(currentValue + "/" + maxValue)}\x1b[0m";
        }
        
        
        
        
        
        public bool IsDayTime()
        {
            DateTime currentTime = DateTime.Now;
            TimeSpan dayStart = new TimeSpan(6, 0, 0); // 6 AM
            TimeSpan dayEnd = new TimeSpan(18, 0, 0); // 6 PM

            if (currentTime.TimeOfDay >= dayStart && currentTime.TimeOfDay < dayEnd)
            {
                return true;
            }
            return false;
        }
        public bool CheckIfNight()
        {
            DateTime currentTime = DateTime.Now;
            int hour = currentTime.Hour;

            if (hour >= 18 || hour < 6)
            {
                return true;
            }
            return false;
        }



        public async Task DisplayRoomInformation(Soul soul, Room room)
        {
            List<string> illusionedSouls = new List<string>();
            string modifiedRoomDescription = room.Description;

            // Calculate the padding needed to center the room.Name
            int roomNameLength = room.Name.Length;
            int totalBorderLength = 74; // Length of the border
            int padding = (totalBorderLength - roomNameLength) / 2;

            // Add the padding and the centered room.Name
            string paddedRoomName = new string(' ', padding) + "\x1b[36m" + room.Name + "\x1b[0m";
            await SendMessage($"\r\n{paddedRoomName}\r");
            await SendMessage(
                "\x1b[38;2;255;105;180m*~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~~====~*\x1b[0m\r");

            bool hasIllusion = false;

            // Check for illusions among all players in the room
            foreach (var player in room.PlayersInRoom)
            {
                if (!string.IsNullOrEmpty(player.IllusionObject))
                {
                    if (!illusionedSouls.Contains(player.Name))
                    {
                        illusionedSouls.Add(player.Name);
                        modifiedRoomDescription +=
                            $" You also see a {player.IllusionObject}."; // Append the illusion description to the modified room description
                        hasIllusion = true;
                    }
                }
            }

            if (hasIllusion)
            {
                await SendMessage($"{WrapText(modifiedRoomDescription, 70)}\r\n");
            }
            else
            {
                await SendMessage($"{WrapText(room.Description, 70)}\r\n");
            }



            if (!room.Indoor)
            {
                if (IsDayTime())
                {
                    await SendMessage("\x1b[33mIt is day.\x1b[0m");
                }
                else
                {
                    await SendMessage("\x1b[34mIt is night.\x1b[0m");
                }
            }

            bool wasNight = _server.IsNight;
            _server.IsNight = CheckIfNight();

            if (wasNight != _server.IsNight)
            {
                await ApplyVampireBonusAsync(_server.IsNight);
            }

            if (!room.Indoor)
            {

                // Get the weather description and isRaining status from the room
                (string weatherDescription, bool isRaining) = await room.GetCurrentWeatherAsync(room);


                // Send a message with the weather description
                await SendMessage($"\rThe current weather is {weatherDescription}\r\n");
            }

            // check if room has graffiti and add message accordingly
            if (!string.IsNullOrEmpty(room.Graffiti))
            {
                await SendMessage($"\r\n\x1b[33mThere's some graffiti that says '{room.Graffiti}'...\x1b[0m\r\n");
            }

// check if there is a portal open in the room and add message accordingly
            if (room.NexusPortal)
            {
                await SendMessage(
                    $"\r\n\x1b[36mA portal to The Nexus hovers in the air, flickering in and out of existence.\x1b[0m\r");
                // check if the player's level is between 1 and 3 and display hint message
                if (soul.Level >= 1 && soul.Level <= 3)
                {
                    await SendMessage(
                        "\x1b[92mHINT: You can type 'enter portal' to travel through portals.\x1b[0m\r\n");
                }
            }
// check if there is a permanent portal in the room and add message accordingly
            else if (room.PermanentPortal != -1)
            {
                Room destinationRoom = _server.GetRoomById(room.PermanentPortal);
                if (destinationRoom != null)
                {
                    await SendMessage(
                        $"\x1b[36mA portal to {destinationRoom.Name} glows steadily in the air.\x1b[0m\r\n");
                }

                // check if the player's level is between 1 and 3 and display hint message
                if (soul.Level >= 1 && soul.Level <= 3)
                {
                    if (destinationRoom != null)
                    {
                        await SendMessage(
                            "\x1b[92mHINT: You can type 'enter portal' to travel through portals.\x1b[0m\r\n");
                    }
                }
            }

            // check if the room is a recall room and add the soulbind statue message
            if (room.CanRecall)
            {
                await SendMessage(
                    $"\r\x1b[38;2;255;105;180mA statue of an adventurer stands proudly, surrounded by a circle of pink flames.\x1b[0m\r\n");
            }
            
            // check if the room has a vending machine and add the message
            if (room.HasVendingMachine)
            {
                await SendMessage(
                    $"\r\x1b[38;2;255;20;147mA Quicksilver vending machine stands here, playing a loop of promotional videos on a holographic display.\x1b[0m\r\n");
            }

            // check if the room is bloody and add the blood puddle message
            if (room.IsBloody)
            {
                await SendMessage(
                    $"\r\x1b[91mBlood forms a small puddle on the ground.\x1b[0m\r\n");
            }

// Display items on the ground
            if (room.GroundItems.Count > 0)
            {
                foreach (var item in room.GroundItems)
                {
                    await SendMessage($"\x1b[38;2;0;255;170m{item.Name} sits on the ground.\r\x1b[0m");

                }
            }

            await SendMessage("\r\x1b[36mSouls:\x1b[0m\r");

            foreach (Soul player in room.PlayersInRoom.GroupBy(p => p.Name)
                         .Select(g => g.OrderByDescending(p => p.Connection.LastActive).First()))
            {
                if (!player.IsHidden && string.IsNullOrEmpty(player.IllusionObject)) // Added a condition to not show players with illusions
                {
                    string inCombat = player.IsAttacking ? " (In Combat)" : "";
                    if (player.IsAnonymous)
                    {
                        await SendMessage($"\x1b[92mAn anonymous {player.Race.RaceName}\x1b[0m{inCombat}\r\n");
                    }
                    else
                    {
                        string gmTag = player.IsGM ? "*GM* " : "";
                        await SendMessage(
                            $"\x1b[38;2;255;105;180m{gmTag}\x1b[0m{player.Name} ({player.Race.RaceName}) [{Soul.GetColoredClassName(player.CurrentClass.Name)}]{inCombat}\r\n");
                    }
                }
            }


            await SendMessage("\r\x1b[36mEntities:\x1b[0m\r");

            int index = 1; // Add an index variable to number the entries
            foreach (NPC npc in room.NPCsInRoom)
            {
                string npcName = npc.Name;
                List<string> npcStatuses = new List<string>();

                if (npc.IsDead)
                {
                    npcStatuses.Add("Dead");
                    npcStatuses.Add($"{npc.Status}");
                }
                else if (npc.IsAttacking)
                {
                    npcStatuses.Add("In Combat");
                }

                if (!npc.HasBeenLooted && npc.IsDead)
                {
                    npcStatuses.Add("Loot");
                }

                if (!string.IsNullOrEmpty(npc.DecompositionStatus))
                {
                    npcStatuses.Add(npc.DecompositionStatus);
                }

                // Add "Petrified" status if the IsPetrified property is true
                if (npc.IsPetrified)
                {
                    npcStatuses.Add("Petrified");
                }

                if (npcStatuses.Count > 0)
                {
                    npcName += " (" + string.Join(") (", npcStatuses) + ")";
                }

                await SendMessage($"[{index}] \x1b[94m{npcName}\x1b[0m\r"); // Add the index number to the output
                index++; // Increment the index for the next NPC
            }

            await SendMessage("\r\n\x1b[36mExits:\x1b[0m\r");
            foreach (var exit in room.Exits)
            {
                await SendMessage($"{exit.Key} - {exit.Value.Name}\r");
            }

            foreach (var altExit in room.AlternativeExits)
            {
                await SendMessage($"{altExit.Key} - {_server.GetRoomById(altExit.Value).Name}\r");
            }



            await SendMessage("\r\n\x1b[36mMap:\x1b[0m\r\n");

            // Check if there are rooms in each direction
            string northRoom = room.Exits.ContainsKey("north") ? "N" : " ";
            string southRoom = room.Exits.ContainsKey("south") ? "S" : " ";
            string eastRoom = room.Exits.ContainsKey("east") ? "E" : " ";
            string westRoom = room.Exits.ContainsKey("west") ? "W" : " ";

            // Display the map using a basic ASCII representation
            string map = $@"
  {northRoom}
{westRoom} X {eastRoom}
  {southRoom}
";
            await SendMessage(map);
            await SendMessage("\r");
            await SendMessage(
                $"HP: {GenerateProgressBar(soul.CurrentHealth, soul.GetTotalMaxHealth(), 20, ConsoleColor.Red)} ({soul.CurrentHealth}/{soul.GetTotalMaxHealth()})\r");
            await SendMessage(
                $"MP: {GenerateProgressBar(soul.CurrentMana, soul.GetTotalMaxMana(), 20, ConsoleColor.DarkCyan)} ({soul.CurrentMana}/{soul.GetTotalMaxMana()})\r");
            await SendMessage(
                $"MV: {GenerateProgressBar(soul.MovementPoints, soul.MaxMovementPoints, 20, ConsoleColor.Green)} ({soul.MovementPoints}/{soul.MaxMovementPoints})\r");
            await SendMessage(
                $"XP: {GenerateProgressBar(soul.CurrentExperience, soul.MaxExperience, 20, ConsoleColor.Magenta)} ({soul.CurrentExperience}/{soul.MaxExperience})\r\n");

            if (soul.Group != null && soul.Group.Members.Count > 1)
            {
                await SendMessage("\r\n\x1b[36mGroup:\x1b[0m\r\n");
                foreach (Soul member in soul.Group.Members)
                {
                    if (member != soul)
                    {
                        await SendMessage(
                            $"- {member.Name} [{Soul.GetColoredClassName(member.CurrentClass.Name)}] ({member.CurrentRoom.Name})\r\n");
                        await SendMessage(
                            $"  HP: {GenerateProgressBar(member.CurrentHealth, member.GetTotalMaxHealth(), 10, ConsoleColor.DarkRed)} ({member.CurrentHealth}/{member.GetTotalMaxHealth()})\r");
                        await SendMessage(
                            $"  MP: {GenerateProgressBar(member.CurrentMana, member.GetTotalMaxMana(), 10, ConsoleColor.DarkBlue)} ({member.CurrentMana}/{member.GetTotalMaxMana()})\r");
                        await SendMessage("\x1b[36m\r\n\x1b[0m");
                    }
                }
            }
        }



        public async Task<string> ReceiveMessage(CancellationToken cancellationToken = default)
        {
            try
            {
                Console.WriteLine("Debug: Before ReadAsync in ReceiveMessage");
                using var reader = new StreamReader(Console.OpenStandardInput());

                var message = await reader.ReadLineAsync().WithCancellation(cancellationToken);
                Console.WriteLine($"Debug: After ReadAsync in ReceiveMessage: '{message}'");
                return message;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Debug: ReceiveMessage canceled");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                return null;
            }
        }


       private async Task InviteToGroup(Soul soul, string playerName)
       {
           if (string.IsNullOrEmpty(playerName))
           {
               await SendMessage("Usage: invite <player name>\r\n");
               return;
           }

           Soul targetPlayer = _server.GetSoulByName(playerName);
           if (targetPlayer == null)
           {
               await SendMessage("Player not found.\r\n");
               return;
           }

           if (soul.Group != null)
           {
               if (soul.Group.Leader != soul)
               {
                   await SendMessage("You are not the leader of your group.\r\n");
                   return;
               }
               else if (soul.Group.Members.Count >= 6)
               {
                   await SendMessage("Your group is already full.\r\n");
                   return;
               }
           }

           if (targetPlayer.Group != null)
           {
               await SendMessage($"{targetPlayer.Name} is already in a group.\r\n");
               return;
           }

           if (soul.Group == null)
           {
               soul.Group = new Group();
               soul.Group.Members.Add(soul);
               soul.Group.Leader = soul;
           }
    
           targetPlayer.GroupInvite = soul.Group;
           await targetPlayer.Connection.SendMessage($"{soul.Name} invited you to a group. Join with 'accept group'.\r\n");
           await SendMessage($"You invited {targetPlayer.Name} to your group.\r\n");
       }



       private async Task AcceptGroupInvite(Soul soul)
       {
           if (soul.GroupInvite == null)
           {
               await SendMessage("You have no pending group invites.\r\n");
               return;
           }

           soul.Group = soul.GroupInvite;
           soul.Group.Members.Add(soul);
           soul.GroupInvite = null;
           await SendMessage("You joined the group.\r\n");

           // Find the group leader (the inviter)
           Soul groupLeader = soul.Group.Members.FirstOrDefault();
           if (groupLeader != null)
           {
               await groupLeader.Connection.SendMessage($"{soul.Name} has joined your group.\r\n");
           }
       }
       
        
        private async Task ShowHelpMenu(string command = null)
        {
            if (!string.IsNullOrEmpty(command))
            {
                if (HelpTopics.TryGetValue(command, out string description))
                {
                    await SendMessage($"\r\n\x1b[1m\x1b[36mHelp for '{command}':\x1b[0m\r\n");
                    await SendMessage($"\x1b[1m\x1b[33m{command}\x1b[0m\x1b[37m: {description}\x1b[0m\r\n");
                    await SendMessage("\r\n\x1b[1m\x1b[36mType 'help' to see the full list of commands.\x1b[0m\r\n");
                }
                else
                {
                    await SendMessage($"\r\n\x1b[1m\x1b[31mError: Command '{command}' not found in help topics.\x1b[0m\r\n");
                }
            }
            else
            {
                await SendMessage("\r\n\x1b[1m\x1b[36m-=[ Help Menu ]=-\x1b[0m\r\n");

                int maxCommandLength = HelpTopics.Keys.Max(cmd => cmd.Length);
                int padding = 2;

                foreach (var helpTopic in HelpTopics)
                {
                    string paddedCommand = helpTopic.Key.PadRight(maxCommandLength + padding);
                    await SendMessage($"\x1b[1m\x1b[33m{paddedCommand}\x1b[0m\x1b[37m{helpTopic.Value}\x1b[0m\r\n");
                }

                await SendMessage("\r\n\x1b[1m\x1b[36mType 'help <command>' for more information about a specific command.\x1b[0m\r\n");
            }
        }




        
        
        private static string WrapText(string text, int width)
        {
            string[] words = text.Split(' ');
            StringBuilder builder = new StringBuilder();
            int lineLength = 0;
            foreach (string word in words)
            {
                if (lineLength + word.Length > width)
                {
                    builder.Append("\r\n");
                    lineLength = 0;
                }

                builder.Append(word + " ");
                lineLength += word.Length + 1;
            }
            return builder.ToString().TrimEnd();
        }
    }
}
