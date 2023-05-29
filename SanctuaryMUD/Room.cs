using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace SanctuaryMUD
{
    public class Room
    {
        private Server _server;
        public string Name { get; set; }
        public int ID { get; set; }
        public string Description { get; set; }
        public Dictionary<string, Room> Exits { get; set; }
        public Dictionary<string, string> ObjectsInRoom { get; private set; } = new Dictionary<string, string>();
        public int North { get; set; }
        public int South { get; set; }
        public int East { get; set; }
        public int West { get; set; }
        public List<Soul> PlayersInRoom { get; private set; } = new List<Soul>();
        public List<NPC> NPCs { get; set; } = new List<NPC>();
        public List<NPC> NPCsInRoom { get; set; } = new List<NPC>();
        public string Graffiti { get; set; }
        public bool NexusPortal { get; set; }
        public bool IsBloody { get; set; }
        public string KeyItem { get; set; }
        // Add the TimedMessage property and the MessageDelay property
        public string TimedMessage { get; set; }
        public int MessageDelay { get; set; }
        public bool Indoor { get; set; }
        public Dictionary<string, string> BooksInRoom { get; set; }
        public string Weather { get; set; }
        public DateTime? WeatherOverrideExpiration { get; set; }
        private readonly Timer _dayNightTimer;
        public bool HasVendingMachine { get; }
        public List<string> RandomRoomMessages { get; set; }
        public TimeSpan? RandomRoomTimer { get; set; }
        public List<ConsoleColor> RandomRoomColors { get; set; }


public List<string> RandomMessages { get; set; } = new List<string>();
    public int RandomMessagesDelay { get; set; }
        public Dictionary<string, int> AlternativeExits { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool CanRecall { get; set; } 
        public int PermanentPortal { get; set; } 
        public bool ContainsSoul(Soul soul)
        {
            return PlayersInRoom.Contains(soul);
        }
        public Dictionary<int, ForageableItem> ForageableItems { get; set; }

        public List<Item> GroundItems { get; set; }




        public Room(string name, int id, string description, Dictionary<int, ForageableItem> forageableItems,
            Dictionary<string, string> objectsInRoom, int north, int south, int east, int west, bool canRecall,
            int permanentPortal = 0, Dictionary<string, int> alternativeExits = null, string keyItem = null,
            string timedMessage = "", int messageDelay = 0, List<string> randomMessages = null,
            int randomMessagesDelay = 0, bool indoor = false, bool hasVendingMachine = false, List<string> randomRoomMessages = null, TimeSpan? randomRoomTimer = null, List<ConsoleColor> randomRoomColors = null)
        {
            Name = name;
            ID = id;
            Description = description;
            Exits = new Dictionary<string, Room>();
            ObjectsInRoom = objectsInRoom;
            North = north;
            South = south;
            East = east;
            West = west;
            CanRecall = canRecall;
            PermanentPortal = permanentPortal;
            NPCsInRoom = new List<NPC>();
            PlayersInRoom = new List<Soul>();
            ForageableItems = forageableItems;
            AlternativeExits = alternativeExits ?? new Dictionary<string, int>();
            KeyItem = keyItem;
            TimedMessage = timedMessage;
            MessageDelay = messageDelay;
            RandomMessages = randomMessages ?? new List<string>();
            RandomMessagesDelay = randomMessagesDelay;
            GroundItems = new List<Item>();
            Indoor = indoor;
            BooksInRoom = new Dictionary<string, string>();
            HasVendingMachine = hasVendingMachine;
            RandomRoomMessages = randomRoomMessages;
            RandomRoomTimer = randomRoomTimer;
            RandomRoomColors = randomRoomColors;
            // Start the timer if RandomRoomTimer is set
            if (RandomRoomTimer.HasValue)
            {
                StartRandomMessageTimer();
            }
        }
        private async void StartRandomMessageTimer()
        {
            while (true)
            {
                await Task.Delay(RandomRoomTimer.Value);

                if (RandomRoomMessages != null && RandomRoomMessages.Count > 0 && RandomRoomColors != null && RandomRoomColors.Count > 0)
                {
                    Random random = new Random();
                    string randomMessage = RandomRoomMessages[random.Next(RandomRoomMessages.Count)];
                    ConsoleColor randomColor = RandomRoomColors[random.Next(RandomRoomColors.Count)];

                    await SendMessageToAllPlayers($"{{color:{randomColor}}}{randomMessage}");
                }
            }
        }

        public NPC GetNPCByName(string npcName)
        {
            Console.WriteLine($"GetNPCByName called with npcName: {npcName}");

            NPC foundNPC = NPCs.FirstOrDefault(npc => StringComparer.OrdinalIgnoreCase.Equals(npc.Name, npcName));

            if (foundNPC != null)
            {
                Console.WriteLine($"NPC found: {foundNPC.Name}");
            }
            else
            {
                Console.WriteLine("No NPC found with the given name.");
            }

            return foundNPC;
        }

        public bool HasVendingMachineAvailable()
        {
            return HasVendingMachine;
        }

        public NPC GetNPCByNameInRoom(string npcName)
        {
            Console.WriteLine($"GetNPCByNameInRoom called with npcName: {npcName}");

            NPC foundNPC = NPCsInRoom.FirstOrDefault(npc => StringComparer.OrdinalIgnoreCase.Equals(npc.Name, npcName));

            if (foundNPC != null)
            {
                Console.WriteLine($"NPC found: {foundNPC.Name}");
            }
            else
            {
                Console.WriteLine("No NPC found with the given name.");
            }

            return foundNPC;
        }
        public bool HasPortal()
        {
            return NexusPortal || PermanentPortal != -1;
        }

        public int GetPortalTargetRoomId()
        {
            if (NexusPortal)
            {
                return 7; // Replace with the Nexus room ID
            }

            return PermanentPortal;
        }



        public void AddExit(string direction, Room room)
        {
            Exits[direction.ToLower()] = room;
        }

        public bool HasExit(string direction)
        {
            return Exits.ContainsKey(direction.ToLower());
        }

        public async void AddPlayerToRoom(Soul soul, Server server, Room room, ClientConnection clientConnection = null)
        {
            List<string> objectList = new List<string> {
                "vase", "candle", "broom", "chair", "table", "book", "pen", "lamp", "mirror", "rug",
                "picture", "clock", "teapot", "plant", "pillow", "blanket", "painting", "sculpture", "telephone", "door",
                "window", "key", "bookcase", "cup", "plate", "fork", "spoon", "knife", "napkin", "cushion",
                "mat", "towel", "soap", "shampoo", "toothbrush", "toothpaste", "razor", "comb", "hairbrush", "tissue",
                "mirror", "perfume", "cosmetics", "shower", "bathtub", "sink", "toilet", "towel rack", "lampshade", "bedspread",
                "nightstand", "alarm clock", "tv", "remote control", "cable box", "game console", "computer", "keyboard", "mouse", "desk",
                "chair", "stapler", "tape dispenser", "notepad", "file cabinet", "calendar", "trash can", "mop", "bucket", "sponge",
                "detergent", "bleach", "fabric softener", "oven", "stove", "microwave", "refrigerator", "freezer", "dishwasher", "toaster",
                "blender", "coffee maker", "kettle", "juicer", "mixer", "food processor", "knife set", "cutting board", "pot", "pan",
                "baking dish", "measuring cup", "measuring spoon", "spatula", "whisk", "ladle", "tongs", "can opener", "bottle opener"
            };
            try
            {
                if (soul.CurrentRoomId > 0)
                {
                    Room oldRoom = server.GetRoomById(soul.CurrentRoomId);
                    if (oldRoom != null)
                    {
                        Console.WriteLine($"Removing player from room {oldRoom.ID}");
                        oldRoom.RemovePlayerFromRoom(soul);
                        soul.RaisePlayerLeavingRoomEvent();
                    }
                }


                PlayersInRoom?.Add(soul);

                SendMessageToAllPlayers($"{soul.Name} has entered the room.", soul);

                soul.CurrentRoomId = ID;
                soul.CurrentRoom = this;

                soul.IsAttacking = false;

                if (!string.IsNullOrEmpty(room.TimedMessage) && room.MessageDelay > 0)
                {
                    System.Timers.Timer messageTimer = new System.Timers.Timer(room.MessageDelay * 1000);
                    messageTimer.AutoReset = false;
                    messageTimer.Elapsed += async (sender, e) =>
                    {
                        if (soul.CurrentRoomId == room.ID)
                        {
                            await soul.Connection?.SendMessage(room.TimedMessage + "\r\n");
                        }

                        messageTimer.Dispose();
                    };
                    messageTimer.Start();
                }

                DateTime now = DateTime.Now;
                bool isDayTime = (now.Hour >= 6) && (now.Hour < 18);

                if (!room.Indoor)
                {
                    if (isDayTime)
                    {
                        soul.ActiveEffects?.Remove("moonlight");

                        if (!(soul.ActiveEffects?.Contains("sunlight") ?? false))
                        {
                            soul.ActiveEffects?.Add("sunlight");
                        }
                    }
                    else
                    {
                        soul.ActiveEffects?.Remove("sunlight");

                        if (!(soul.ActiveEffects?.Contains("moonlight") ?? false))
                        {
                            soul.ActiveEffects?.Add("moonlight");
                        }
                    }
                }
                else
                {
                    soul.ActiveEffects?.Remove("sunlight");
                    soul.ActiveEffects?.Remove("moonlight");
                }
                var currentRoom = this;
                ApplyDayNightBuffs(server, currentRoom, soul);



            
        if (room.RandomMessages?.Any() ?? false && room.RandomMessagesDelay > 0)
        {
            System.Timers.Timer randomMessageTimer = new System.Timers.Timer(room.RandomMessagesDelay * 1000);
            randomMessageTimer.AutoReset = true;
            randomMessageTimer.Elapsed += async (sender, e) =>
            {
                if (soul.CurrentRoomId == room.ID)
                {
                    Random random = new Random();
                    int randomIndex = random.Next(room.RandomMessages.Count);
                    string randomMessage = room.RandomMessages[randomIndex];
                    await soul.Connection?.SendMessage(randomMessage + "\r\n");
                }
            };
            randomMessageTimer.Start();
            if (soul.Connection != null)
            {
                soul.Connection.Disconnected += (sender, e) => randomMessageTimer.Dispose();
            }
        }
            }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while adding a player to a room: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

        



        public void AddObjectToRoom(string objectName, string description)
        {
            ObjectsInRoom[objectName.ToLower()] = description;
        }

        public bool HasObject(string objectName)
        {
            return ObjectsInRoom.ContainsKey(objectName.ToLower());
        }

        public string GetObjectDescription(string objectName)
        {
            string description = "";
            ObjectsInRoom.TryGetValue(objectName.ToLower(), out description);
            return description;
        }
        public async Task<(string, bool)> GetCurrentWeatherAsync(Room room)
        {
            if (room.WeatherOverrideExpiration.HasValue && DateTime.UtcNow < room.WeatherOverrideExpiration.Value)
            {
                return (room.Weather, true);
            }

            string apiKey = "d4745b51c6b5ba278ee5abc839d6995d";
            string zipCode = "47167";
            string country = "us";
            string apiUrl =
                $"https://api.openweathermap.org/data/2.5/weather?zip={zipCode},{country}&appid={apiKey}&units=imperial";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject weatherData = JObject.Parse(responseBody);
                    string weatherDescription = weatherData["weather"][0]["description"].ToString();
                    return (weatherDescription, false);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Error fetching weather data: {e.Message}");
                    return ("unknown", false);
                }
            }
        }

public async Task ApplyDayNightBuffs(Server server, Room currentRoom, Soul soul)
{
    DateTime now = DateTime.Now;
    string weatherDescription;
    bool isOverridden;

    try
    {
        (weatherDescription, isOverridden) = await GetCurrentWeatherAsync(currentRoom);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting weather for room ID {currentRoom.ID}: {ex.Message}");
        return;
    }

    bool isCloudy = Regex.IsMatch(weatherDescription, @"(overcast|clouds|cloudy)", RegexOptions.IgnoreCase);

    if (currentRoom.Indoor)
    {
        soul.ActiveEffects?.Remove("sunlight");
        soul.ActiveEffects?.Remove("moonlight");
        soul.ActiveEffects?.Remove("cloudy skies");
        //soul.Connection.SendMessage("\x1b[93mYou are indoors, so all outdoor effects have been removed.\x1b[0m\r\n");
    }
    else
    {
        bool isDayTime = (now.Hour >= 6) && (now.Hour < 18);

        if (isDayTime)
        {
            soul.ActiveEffects?.Remove("moonlight");
            if (!(soul.ActiveEffects?.Contains("sunlight") ?? false))
            {
                soul.ActiveEffects?.Add("sunlight");
                soul.Connection.SendMessage(
                    "\033[38;2;255;255;0mThe sun rises and its warmth invigorates you\x1b[0m\r\n");
            }

            if (soul.ActiveEffects?.Contains("cloudy skies") ?? false)
            {
                soul.ActiveEffects?.Remove("cloudy skies");
                soul.Connection.SendMessage(
                    "\x1b[93mThe skies have cleared, and the sunlight invigorates you more.\x1b[0m\r\n");
            }
        }
        else
        {
            soul.ActiveEffects?.Remove("sunlight");
            if (!(soul.ActiveEffects?.Contains("moonlight") ?? false))
            {
                soul.ActiveEffects?.Add("moonlight");
                soul.Connection.SendMessage(
                    "\x1b[35mThe night falls and the moonlight enhances your magical abilities.\r\n\x1b[0m");
            }

            if (isCloudy && !(soul.ActiveEffects?.Contains("cloudy skies") ?? false))
            {
                soul.ActiveEffects?.Add("cloudy skies");
                soul.Connection.SendMessage(
                    "\x1b[93mThe skies are cloudy, but you still feel slightly invigorated.\x1b[0m\r\n");
            }
        }
    }
}




        public void UpdateNPCAttackingStatus()
        {
            foreach (NPC npc in NPCsInRoom)
            {
                if (npc.CurrentTarget != null && !PlayersInRoom.Contains(npc.CurrentTarget as Soul))
                {
                    npc.IsAttacking = false;
                    npc.CurrentTarget = null;
                    Console.WriteLine($"NPC {npc.Name} ({npc.ID}) stopped attacking as its target is no longer in the room.");
                }
            }
        }

        public void RemovePlayerFromRoom(Soul soul)
        {
            // Set IsAttacking to false when leaving the room
            soul.IsAttacking = false;

            PlayersInRoom.Remove(soul);

            if (PlayersInRoom.Count == 0)
            {
                Console.WriteLine($"No souls left in room {ID}");

                // Set IsAttacking to false for all NPCs in the room
                foreach (NPC npc in NPCsInRoom)
                {
                    npc.IsAttacking = false;
                    npc.CurrentTarget = null;
                    Console.WriteLine($"NPC {npc.Name} ({npc.ID}) stopped attacking as there are no souls left in the room.");
                }
            }
            else
            {
                // Check if any NPCs need to update their attacking status
                UpdateNPCAttackingStatus();
            }
        }

        public async Task SendMessageToAllPlayers(string message, Soul senderSoul = null, bool isEmote = false, bool isSayCommand = false)
        {
           // Console.WriteLine($"Sending message: {message}");

            foreach (Soul player in PlayersInRoom)
            {
                if (player != senderSoul)
                {
                    Console.WriteLine($"Sending message to player: {player.Name}");
                    await player.Connection.SendMessage($"{message}\r\n");
                }
            }
        }

        public List<Room> GetConnectedRooms(Server server)
        {
            return Exits.Values.ToList();
        }
        public async Task AddNPC(NPC npc, Server server)
        {
            // Check if another NPC with the same name already exists in the room
            var existingNPC = NPCsInRoom.FirstOrDefault(x => x.Name == npc.Name);
            if (existingNPC != null)
            {
                // Find a different room on the NPC's allowed rooms list to add it to
                var allowedRooms = npc.AllowedRooms.Where(x => x != ID && server.GetRoomById(x) != null);
                if (allowedRooms.Any())
                {
                    var nextRoom = server.GetRoomById(allowedRooms.First());
                    await nextRoom.AddNPC(npc, server);
                    return;
                }
                else
                {
                    Console.WriteLine($"No available rooms to move NPC '{npc.Name}' with ID {npc.ID}.");
                }
            }

            // Add the NPC to the room
            NPCsInRoom.Add(npc);
            await SendMessageToAllPlayers($"\x1b[94m{npc.Name} enters the room.\x1b[0m", null);
        } 



        public async Task RemoveNPC(NPC npc, Server server)
        {
            NPCsInRoom.Remove(npc);
            await SendMessageToAllPlayers($"\x1b[94m{npc.Name} leaves the room.\x1b[0m", null);
        }


        public List<string> GetExits()
        {
            var exits = new List<string>();

            foreach (var exit in Exits)
            {
                var direction = exit.Key;
                var roomName = exit.Value.Name;
                exits.Add($"{direction} - {roomName}");
            }

            return exits;
        }


    }
}
