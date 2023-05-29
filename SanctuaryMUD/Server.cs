using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Timers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Timer = System.Threading.Timer;

namespace SanctuaryMUD
{
    public class Server
    {
        private TcpListener _listener;
        private List<ClientConnection> _clients;
        private readonly object _clientsLock = new object();
        private Timer _timer;
        private System.Timers.Timer _disconnectionCheckTimer;
        public List<Soul> Souls { get; set; }
        private List<NPC> _npcs;
        public Dictionary<int, DateTime> ActivePortals { get; private set; }
        public ConcurrentDictionary<int, DateTime> NpcRespawnTimers { get; private set; } = new ConcurrentDictionary<int, DateTime>();
        public List<NPC> NPCs { get; set; }
        private System.Timers.Timer _duplicateCheckTimer;
        private int _highestNPCId; 
        private System.Timers.Timer _saveSoulTimer;
        private Timer _cleanupTimer;
        public bool IsNight { get; set; }
        private Timer _updateTimer;

        
        public Server(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _clients = new List<ClientConnection>();
            Souls = new List<Soul>(); // Initialize the Souls list here
            InitializeRooms();
            InitializeNPCs();
            NPCs = new List<NPC>();
            // Create a timer that will execute the RegenerateHealth method every minute
            _timer = new Timer(RegenerateHealth, null, 0, 9000); // 60000 milliseconds = 1 minute
            // Initialize and configure the Timer to remove disconnected players from lists
            _disconnectionCheckTimer = new System.Timers.Timer(10000); // Check every 10 seconds
            _disconnectionCheckTimer.Elapsed += CheckDisconnectedPlayers;
            _disconnectionCheckTimer.AutoReset = true;
            _disconnectionCheckTimer.Enabled = true;
            // Initialize and configure the Timer to check for duplicate NPCs
            _duplicateCheckTimer = new System.Timers.Timer(2000); // Check every 2 seconds
            _duplicateCheckTimer.Elapsed += CheckDuplicateNPCs;
            _duplicateCheckTimer.AutoReset = true;
            _duplicateCheckTimer.Enabled = true;
            StartDuplicateSoulCheckTimer();
            StartNpcRespawnChecking();
            ActivePortals = new Dictionary<int, DateTime>();
            _ = ProcessCorpseStates(); // Start the coroutine.
            // Initialize and configure the Timer to save connected clients' souls every 3 seconds
            _saveSoulTimer = new System.Timers.Timer(3000); // Check every 3 seconds (3000 milliseconds)
            _saveSoulTimer.Elapsed += SaveAllConnectedSouls;
            _saveSoulTimer.AutoReset = true;
            _saveSoulTimer.Enabled = true;
            _cleanupTimer = new Timer(HandleForcefulDisconnections, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            _updateTimer = new Timer(OnUpdateTimerElapsed, null, 0, 1000);
        }
        private void OnUpdateTimerElapsed(object state)
        {
            UpdateMaxHealthAndManaForAllSouls();
        }

        private void UpdateMaxHealthAndManaForAllSouls()
        {
            var onlineSouls = this.Souls;

            foreach (Soul soul in onlineSouls)
            {
                soul.GetTotalMaxHealth();
                soul.GetTotalMaxMana();
            }
        }
    
        
        public async Task UpdateRespawnTimers()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); // Check every second

                var currentTime = DateTime.UtcNow;
                var npcsToRespawn = NpcRespawnTimers
                    .Where(kvp => kvp.Value <= currentTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var npcId in npcsToRespawn)
                {
                    if (NpcRespawnTimers.TryRemove(npcId, out DateTime respawnTime))
                    {
                        await RespawnNpc(npcId);
                    }
                }
            }
        }
        
        private void HandleForcefulDisconnections(object state)
        {
            // Iterate through all clients
            foreach (var client in _clients.ToList())
            {
                // Check if the client is forcefully disconnected
                if (!client.IsConnected)
                {
                    try
                    {
                        // Handle the client's graceful logout
                        Console.WriteLine($"Forcefully disconnected client {client.Soul.Name} is being removed.");
                        _clients.Remove(client);
                        if (client.Soul != null)
                        {
                            client.Soul.Connection = null;
                            Souls.Remove(client.Soul);
                            SaveSoul(client.Soul);
                        }
                    }
                    catch (NullReferenceException ex)
                    {
                        // Handle the exception gracefully
                        Console.WriteLine($"An error occurred while handling disconnection for client : {ex.Message}");
                        continue;
                    }
                }
            }
        }


        
        private void SaveAllConnectedSouls(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    if (client.Soul != null)
                    {
                        SaveSoul(client.Soul);
                    }
                }
            }
        }
  
        
        private void CheckDuplicateNPCs(Object source, ElapsedEventArgs e)
        {
            // Iterate over all rooms
            foreach (Room room in _rooms)
            {
                // Create a dictionary to store NPCs by name
                Dictionary<string, List<NPC>> npcDictionary = new Dictionary<string, List<NPC>>();

                // Iterate over NPCs in the room
                foreach (NPC npc in room.NPCsInRoom)
                {
                    // If an NPC with the same name is already in the dictionary, remove the NPC from the room
                    if (npcDictionary.ContainsKey(npc.Name))
                    {
                        Console.WriteLine($"Duplicate NPC '{npc.Name}' found in room {room.ID}.");
                        room.RemoveNPC(npc, this);
                    }
                    else // Otherwise, add the NPC to the dictionary
                    {
                        npcDictionary.Add(npc.Name, new List<NPC>() { npc });
                    }
                }
            }
        }


        
        public async Task RespawnNpc(int npcId)
        {
            NPC originalNpc = GetNpcById(npcId);
            if (originalNpc != null)
            {
                int newId = GenerateUniqueNPCId();
                string baseName = Regex.Replace(originalNpc.Name, @"\s*\d+$", ""); // Remove any digits and preceding space at the end of the name
                string newName = $"{baseName} {newId}";

                Console.WriteLine($"Respawning NPC '{originalNpc.Name}' with ID {originalNpc.ID}");
                NPC newNpc = new NPC(
                    this,
                    newId,
                    newName,
                    originalNpc.Level,
                    originalNpc.StartingRoomId,
                    originalNpc.MaxHealth,
                    originalNpc.MaxMana,
                    originalNpc.MaxHealth,
                    originalNpc.MaxMana,
                    originalNpc.Description,
                    originalNpc.AllowedRooms,
                    originalNpc.AttackSpeed,
                    originalNpc.AttackDamage,
                    originalNpc.LootList,
                    originalNpc.Dialogue,
                    originalNpc.IsTrainer,
                    originalNpc.RespawnTimer
                    
                );

                Room startingRoom = GetRoomById(originalNpc.StartingRoomId);
                if (startingRoom != null)
                {
                    Console.WriteLine($"Adding respawned NPC '{newNpc.Name}' with ID {newNpc.ID} to room with ID {startingRoom.ID}");
                    await startingRoom.AddNPC(newNpc, this);
                    _npcs.Add(newNpc);
                    // Start the NPC movement task
                    Task.Run(() => newNpc.MoveBetweenRooms(this));
                }
                else
                {
                    Console.WriteLine($"Could not find room with ID {originalNpc.StartingRoomId} for respawning NPC '{originalNpc.Name}' with ID {originalNpc.ID}");
                }
            }
            else
            {
                Console.WriteLine($"Could not find NPC with ID {npcId} to respawn");
            }
        }

        private int GetUniqueNumberForRespawnedNpc(string npcName)
        {
            int uniqueNumber = 1;
            Regex regex = new Regex($"^{npcName}(?: (\\d+))?$");

            List<int> existingNumbers = new List<int>();

            foreach (NPC npc in _npcs)
            {
                Match match = regex.Match(npc.Name);
                if (match.Success)
                {
                    int currentNumber = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 1;
                    existingNumbers.Add(currentNumber);
                }
            }

            // Find the first available unique number
            while (existingNumbers.Contains(uniqueNumber))
            {
                uniqueNumber++;
            }

            return uniqueNumber;
        }


        public async Task CheckNpcRespawnTimers()
        {
            DateTime currentTime = DateTime.UtcNow;
            List<int> npcsToRespawn = new List<int>();

            foreach (var entry in NpcRespawnTimers)
            {
                if (currentTime >= entry.Value)
                {
                    npcsToRespawn.Add(entry.Key);
                }
            }

            foreach (int npcId in npcsToRespawn)
            {
                await RespawnNpc(npcId);
                NpcRespawnTimers.TryRemove(npcId, out _);
            }
        }

        public NPC GetNpcById(int npcId)
        {
            // Assuming you have a list of NPCs called _npcs in the Server class
            return _npcs.FirstOrDefault(npc => npc.ID == npcId);
        }
        private int _lastNpcId = 0;

        // Generate a unique NPC ID
        public int GenerateUniqueNPCId()
        {
            _highestNPCId++; // Increment the highest NPC ID
            return _highestNPCId;
        }
        public async Task StartNpcRespawnChecking()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5)); // Check every 5 seconds
                await CheckNpcRespawnTimers();
            }
        }
        public bool IsDayTime()
        {
            var now = DateTime.UtcNow;
            var sunrise = new DateTime(now.Year, now.Month, now.Day, 6, 0, 0);
            var sunset = new DateTime(now.Year, now.Month, now.Day, 18, 0, 0);
            return now > sunrise && now < sunset;
        }

        private void CheckDisconnectedPlayers(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (Souls)
            {
                var disconnectedClients = Souls.Where(s => !s.Connection.IsConnected).Select(s => s.Connection).ToList();

                foreach (var client in disconnectedClients)
                {
                    if (client.Soul != null)
                    {
                        client.PlayerLoggedOut(client.Soul).Wait();
                    }
                    client.CancelToken();
                }
            }
        }

        
private void RegenerateHealth(object state)
{
    // Iterate through all players and regenerate their health and mana
    foreach (var soul in Souls)
    {
        RegenerateEntity(soul);
    }

    // Iterate through all NPCs and regenerate their health and mana
    foreach (var npc in NPCs)
    {
        RegenerateEntity(npc);
    }
}

private void RegenerateEntity(Entity entity)
{
    bool healthChanged = false;
    bool manaChanged = false;
    bool movementChanged = false;
    Soul soul = null;
    
    int previousHealth = entity.CurrentHealth;
    int previousMana = 0;
    int previousMovementPoints = entity.MovementPoints;

    if (entity is Soul)
    {
        soul = entity as Soul;
        previousMana = soul.CurrentMana;

        // Remove expired effects
        foreach (var effect in soul.ActiveEffects.ToList())
        {
            if (soul.ActiveEffectExpirationTimes.TryGetValue(effect, out DateTime expirationTime) && expirationTime <= DateTime.UtcNow)
            {
                soul.ActiveEffects.Remove(effect);
                soul.ActiveEffectExpirationTimes.Remove(effect);
            }
        }

        int healthRegenModifier = soul.HealthRegenModifier;

        if (soul.ActiveEffects.Contains("cellular regeneration"))
        {
            healthRegenModifier += 2;
        }
        if (soul.ActiveEffects.Contains("photosynthesis"))
        {
            healthRegenModifier += 1;
        }

        if (soul.ActiveEffects.Contains("blood poisoning"))
        {
            healthRegenModifier -= 2;
        }

        int maxHealth = soul.GetTotalMaxHealth();
        int maxMana = soul.GetTotalMaxMana();

        if (soul.CurrentHealth < maxHealth)
        {
            soul.CurrentHealth += Math.Max(1 + healthRegenModifier, 0);
            healthChanged = true;
        }



// Regenerate movement points for the Soul
        if (soul.MovementPoints < soul.MaxMovementPoints)
        {
            soul.MovementPoints += 1; // Modify this value to change the regeneration rate

            // Add 2 extra movement points per tick if the player is sitting
            if (soul.IsSitting)
            {
                soul.MovementPoints += 2;
            }

            // Ensure that MovementPoints doesn't exceed MaxMovementPoints
            if (soul.MovementPoints > soul.MaxMovementPoints)
            {
                soul.MovementPoints = soul.MaxMovementPoints;
            }

            movementChanged = true;
        }


        if (soul.CurrentHealth < maxHealth)
        {
            if (!(soul.CurrentClass.Name == "Vampire" && IsDayTime()))
            {
                soul.CurrentHealth += Math.Max(1 + healthRegenModifier, 0);
                healthChanged = true;
            }
        }

        if (soul.CurrentMana <= maxMana)
        {
            int manaToAdd = soul.CosmicEssenceActive ? 2 : 1;

            // Add 1 additional mana regeneration for "Mana Elf" race
            if (soul.Race.RaceName == "Mana Elf")
            {
                manaToAdd += 1;
            }

            soul.CurrentMana += Math.Max(manaToAdd + soul.ManaRegenModifier, 0);

            // Ensure that CurrentMana doesn't exceed MaxMana
            if (soul.CurrentMana > maxMana)
            {
                soul.CurrentMana = maxMana;
            }

            manaChanged = true;
        }
    }
    else
    {
        // Regenerate health for non-Soul entities
        if (entity.CurrentHealth < entity.MaxHealth)
        {
            entity.CurrentHealth += Math.Max(1 + entity.HealthRegenModifier, 0);
            healthChanged = true;
        }
    }

    if (healthChanged || manaChanged || movementChanged)
    {
        if (entity is Soul)
        {
            int maxHealth = soul.GetTotalMaxHealth();
            int maxMana = soul.GetTotalMaxMana();

            if (soul.CurrentHealth != maxHealth || soul.CurrentMana != maxMana)
            {
                // Only show the progress bars if the current value doesn't equal the max value
                var clientConnection = _clients.FirstOrDefault(c => c.Soul == soul);
                if (clientConnection != null)
                {
                    if (healthChanged && soul.CurrentHealth != previousHealth)
                    {
                        clientConnection.SendMessage($"\rHP: {ClientConnection.GenerateProgressBar(soul.CurrentHealth, maxHealth, 20, ConsoleColor.Red)} ({soul.CurrentHealth}/{maxHealth})\r\n");
                    }

                    if (manaChanged && soul.CurrentMana != previousMana)
                    {
                        clientConnection.SendMessage($"\rMP: {ClientConnection.GenerateProgressBar(soul.CurrentMana, maxMana, 20, ConsoleColor.DarkCyan)} ({soul.CurrentMana}/{maxMana})\r\n");
                    }
                    if (movementChanged && soul.MovementPoints != previousMovementPoints)
                    {
                        clientConnection.SendMessage($"\rMV: {ClientConnection.GenerateProgressBar(soul.MovementPoints, soul.MaxMovementPoints, 20, ConsoleColor.Green)} ({soul.MovementPoints}/{soul.MaxMovementPoints})\r\n");
                    }
                }
            }
        }
    }
}




        private void StartDuplicateSoulCheckTimer()
        {
            var timer = new Timer(state => {
                // Check for duplicate souls
                var duplicateSouls = Souls.GroupBy(s => s.Name)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.OrderByDescending(s => s.Connection.LastActive, Comparer<DateTime>.Default));

                // Remove duplicate souls except for the most recent one
                foreach (var duplicateSoul in duplicateSouls.Skip(1))
                {
                    Souls.Remove(duplicateSoul);
                    Console.WriteLine($"Removed duplicate soul '{duplicateSoul.Name}'");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
        }




        public async Task Start()
        {
            InitializeDatabase();
            _listener.Start();
            Console.WriteLine("Server started. Listening for connections...");

            while (true)
            {
                TcpClient client = _listener.AcceptTcpClient();
                Console.WriteLine("Client connected.");
                ClientConnection clientConnection = new ClientConnection(client, this);
                _clients.Add(clientConnection);

                Task.Run(async () => await clientConnection.HandleClient());
            }
        }

private void InitializeRooms()
{
var seasideForagableItems = new Dictionary<int, ForageableItem>
{
    { 5, new ForageableItem(5, 60) }, // Seashell
    { 6, new ForageableItem(6, 40) }, // Driftwood
    { 7, new ForageableItem(7, 30) }  // Sea Glass
};

    
var cemeteryForagableItems = new Dictionary<int, ForageableItem>
{
    { 8, new ForageableItem(8, 60) }, // Bone Chips
    { 9, new ForageableItem(9, 40) }, // Dirty Flesh
    { 10, new ForageableItem(10, 30) }  // Rimeweed
};

    
    var noForagableItems = new Dictionary<int, ForageableItem>
    {
        
        {11, new ForageableItem(11, 1) }  // Worthless Rock
        
    };
    
    List<string> seasideSounds = new List<string>
    {
        "You hear waves lapping at the shore",
        "You hear a seagull's cry echoing in the distance",
        "You hear a gentle breeze whispering through the shoreline",
        "You hear the distant creak of a ship's rigging",
        "You hear a splash of water as a fish leaps from the waves",
        "You hear children's laughter floating on the wind",
        "You hear a faint, rhythmic thud of an oar striking the water",
        "You hear the rustle of beach grass in the gentle wind",
        "You hear the distant call of a lookout high in a crow's nest",
        "You hear the melodic tune of a bard drifting on the breeze",
        "You hear the crunch of footsteps on the sand",
        "You hear a sailor shouting as they catch a gust of wind",
        "You hear a sail flapping in the wind as a ship passes by",
        "You hear the distant cry of a mythical creature playing in the surf",
        "You hear a couple's quiet conversation drifting over from a beach camp",
        "You hear the soft, soothing sound of the tide ebbing and flowing",
        "You hear the distant hum of a ship's magical engine fading into the background",
        "You hear the joyous shouts of adventurers enjoying the water",
        "You hear the peaceful sound of the wind brushing against the shoreline",
        "You hear the rhythmic creak of a wooden dock swaying in the waves",
        "You hear the distant chime of a treasure hunter's magical detector searching for hidden riches"
    };


    List<ConsoleColor> lightBlueShades = new List<ConsoleColor>
    {
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.White,
        ConsoleColor.Gray,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkBlue,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.White,
        ConsoleColor.Gray,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkBlue,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
        ConsoleColor.White,
        ConsoleColor.Gray,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkBlue,
        ConsoleColor.Cyan,
        ConsoleColor.Blue,
    };


    TimeSpan randomRoomTimer = TimeSpan.FromMinutes(new Random().Next(20, 41));
    
    _rooms = new List<Room>
    
    {
        new Room(
            name: "Levi's Penthouse",
            id: 1,
            description: "Standing on gleaming white marble floors, you look out the wall-sized windows and notice you're \x1b[36mhigh above... somewhere\x1b[0m. You see the tops of skyscrapers and sparkling city lights, but none of the buildings look familiar to you. A shimmering, glittery wind swirls around the tower you stand in, glimmering dust playing against the backlit windows. A large \x1b[33mGorgon\x1b[0m icon serves as a floor rug, perhaps a utilization of evil to ward off other evils. In fact, you notice there are many \x1b[33mGorgon\x1b[0m motifs in this entire penthouse, so if superstitions are to be believed, the resident should be very well protected from all harm. A modern white leather sofa sits in the middle of the room, next to a black leather lounge. A huge \x1b[35mpink moon\x1b[0m looms in the celestial night sky, \x1b[222mpale ruby light\x1b[0m washing into the apartment from the windows.",
            objectsInRoom: new Dictionary<string, string>
            {
                { "gorgon icon", "The Gorgon icon is a large, lifelike sculpture of a gorgon's head, with glowing red eyes and a twisted, snarling mouth." },
                { "moon", "The enormous pink moon casts a pale ruby light that floods the room, casting everything in a gentle, otherworldly glow." }
            },
            north: 2, //powder room
            south: 5, //hallway
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true


        ),
        new Room(
            name: "Levi's Powder Room",
            id: 2,
            description: "This is a large bathroom with glossy black floors you can see your reflection in. A white porcelain sink sits under a large mirrored vanity lined in brightly glowing manastone strips. Expensively perfumed handsoaps and lotions sit on the vanity, ready for use.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 3, //closet
            south: 1, //penthouse
            east: -1,
            west: 6, //shower
            canRecall: false,
            forageableItems: noForagableItems,
        indoor: true


        ),
        new Room(
            name: "Levi's Closet",
            id: 3,
            description: "This is a very brightly lit closet with sparkling white walls, glittering with shiny manastone dust. Rows and rows of black floating shelves line the walls, filled with fashion-related books, accessories such as masks, small decor pieces, candles, and many bottles of fragrances.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 2, //powder room
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
        indoor: true


        ),
        new Room(
            name: "Starlight Towers - Elevator - Top Floor",
            id: 4,
            description: "As you step into the elevator, you feel a sudden shift in the air around you. The room seems to shimmer and distort, and you realize that you have entered a strange and otherworldly space. The walls of the elevator are made of a shimmering black metal, etched with intricate symbols that seem to pulse with a strange energy. The floor is a smooth, glossy surface that seems to reflect the starry expanse of space itself.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 5, //top floor hallway
            south: -1,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true



        ),
        new Room(
            name: "Starlight Towers - Top Floor",
            id: 5,
            description: "You are greeted with a long, wide hallway that seems to stretch on for miles. The walls are made of a glistening, pearlescent material that shimmers in the light, and the floor is a smooth, polished marble that glows softly in the dim lighting. The door to Levi’s Penthouse Suite is lined with a dark, rich wood and are adorned with intricate carvings of celestial beings and otherworldly creatures. The handles are made of polished silver and glow with a faint, ethereal light. The air seems to be infused with a sweet, floral scent. You can't quite place the origin of the scent, but it's pleasant and calming, making you feel at ease. At the end of the hallway, you see a set of double doors made of gleaming steel. Above the doors is a holographic display that shows a 3D model of the apartment building below. The display shows the different levels of the building, and the various amenities available to the residents.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 1, //penthouse
            south: 4, //elevator
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true


        ),
        new Room(
            name: "Levi's Shower",
            id: 6,
            description: "As you step into the black marble shower, your eyes are immediately drawn to the large showerhead, unlike anything you have ever seen before. Instead of a sleek black curve, it is designed to look like the head of a horned goat, its mouth open wide as water cascades out from between its razor-sharp teeth. The goat's head showerhead is a work of art in itself, with lifelike details etched into every inch of its surface. The horns curve gracefully upwards, while the eyes gleam with an otherworldly light. The water that pours out of the goat's mouth is warm and fragrant, infused with the scent of sandalwood and jasmine. As you stand under the shower, you can't help but feel the power and energy of the mystical creature above you. The water pressure is strong, but instead of feeling like a typical massage, it feels like a powerful force that is cleansing your mind and body of any negativity. The floor of the shower is still made of small, smooth black stones that massage your feet as you stand. The stones are cool to the touch, but quickly warm up as the water flows over them. The stones are also infused with small flecks of gold and silver, which shimmer in the soft light. The shower is also equipped with steam vents that fill the air with a fragrant mist, creating a relaxing and spa-like atmosphere. The black marble walls of the shower are adorned with intricate carvings of occult symbols, adding to the otherworldly feeling of the space.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 2, // powder room
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true



        ),
        new Room(
            name: "The Nexus",
            id: 7,
            description: "The Nexus is a vast and mysterious network of interconnected floating islands, suspended in the dark void of deep space. The islands vary in size, shape, and composition, and are linked together by an intricate system of bridges, tunnels, and portals. The landscape is a surreal blend of natural beauty and otherworldly technology, with shimmering crystals, pulsing energy fields, and strange, glowing structures dotting the landscape.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 8,
            south: 12,
            east: 58,
            west: 38,
            canRecall: true,
            forageableItems: noForagableItems


        ),
        new Room(
            name: "Outside the S`Aquia Portal",
            id: 8,
            description: "Connected to the main hub via a translucent crystal bridge, this small island serves as the entry to S`Aquia, City of the Sirens. A white marble platform sits in the center of the island, crafted from the famous siren marble that comprises the city's main export. Through the glowing portal hovering above the platform, you can hear the sounds of the ocean calling to you.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 7,
            east: -1,
            west: -1,
            canRecall: true,
            permanentPortal: 9,
            forageableItems: noForagableItems


        ),
        new Room(
            name: "S`Aquia Docks",
            id: 9,
            description: "The white marble docks stretch out along the coastline, glistening in the sun, and a cool ocean breeze can be felt as you approach. To the north, an eerie ghost ship with tattered sails can be seen docked, waiting to take daring adventurers on a treacherous journey to Telvaz, the Misery Mire. To the east, a sturdy oaken ship is anchored, with ropes and pulleys creaking in the wind. It looks like it's ready to set sail for the Rushforth Ocean, where untold dangers and riches await. Despite the dangers that lurk beyond the docks, the area is kept safe by watchful efreeti guards, their red eyes scanning the surroundings for any signs of trouble. The docks are adorned with beautiful blue cherry blossom trees that are in full bloom, their delicate petals dancing in the wind. The scent of the blossoms fills the air, making it a truly magical experience.",
            objectsInRoom: new Dictionary<string, string>
            {
                { "oaken ship", "A group of burly sailors can be seen loading barrels and crates onto the ship, their muscles bulging with the effort." },
                { "ghost ship", "Its spectral form is eerily translucent, and you can see the mist swirling around it. This ship is bound for Telvaz, a land shrouded in mystery and danger. The ghost ship is said to be crewed by the spirits of the dead, and only the bravest adventurers dare to board it."},
                { "efreeti", "They keep a watchful eye on the comings and goings of the ships and their passengers. They stand tall and proud, their skin glistening like molten lava in the sun."}
            },
            north: -1,
            south: 10, //seaside path
            east: -1,
            west: -1,
            canRecall: false,
            permanentPortal: 8,
            forageableItems: noForagableItems


        ),
        new Room(
            name: "Seaside Path",
            id: 10,
            description: "As you walk, you notice nectarnut trees lining the shore, their branches heavy with sweet fruit. You can hear the sound of waves lapping against the shore, and the salty tang of the sea fills the air. Beyond the shoreline, you see blue cherry blossom trees growing along the green hills. Their delicate petals flutter in the breeze, and their branches sway gently. You feel a sense of calm and tranquility as you take in the beauty of the scenery. As you continue along the path, you see a white marble palace in the distance. It rises up from the landscape, gleaming in the sunlight. You notice a bustling trade district coming up to the south. The area is filled with activity, with siren shoppers and their thralls shopping from siren merchants selling all varieties of seaside goods.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 9, //docks
            south: 13, //trade district
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems

        ),
        new Room(
            name: "Xuolia Library",
            id: 11,
            description: "The ceiling is so high that it disappears into darkness, and the walls are lined with countless rows of towering \x1b[94mbookshelves\x1b[0m, all filled with ancient tomes and mysterious artifacts. The air is thick with the smell of old parchment and the soft whispers of scholars poring over their studies. As you make your way deeper into the library, you notice that each section is devoted to a particular subject, with intricate murals and tapestries adorning the walls and ceilings to depict the topic at hand. From astronomy and astrophysics to quantum mechanics and hyperdimensional mathematics, the Xuolia library has an exhaustive collection of knowledge on every conceivable subject.",
            objectsInRoom: new Dictionary<string, string>()
            {
                { "bookshelves", "A few nearby titles catch your attention: Leviathan's Penthouse Suite For \x1b[94mKids\x1b[0m, \x1b[92mLeviathan\x1b[0m's Penthouse Suite, Lintel's \x1b[93mJournal\x1b[0m, \x1b[38;2;255;105;180mAvatara\x1b[0m Couture, An Aurizari Guide to \x1b[93mGold\x1b[0m Grading" },
                { "kids", "BOOK: Once upon a time, in a magical land far, far away, there was a kind Gorgon named Leviathan. He was unlike any other Gorgon, with long, writhing hair made up of snakes, each one hissing and slithering around his head like a living crown. Leviathan loved to wear beautiful clothes and jewelry, made of the best materials and adorned with rare and precious gems. He was a creature of style and taste, and he loved to show off his beautiful things.\r\nOne day, some brave children sneaked into Leviathan's penthouse suite. They were amazed by the beautiful room they found themselves in. The walls were made of a shimmering material that seemed to glow with an inner light, and the floors reflected the light in a dazzling array of colors. As they explored the room, they saw that the walls were covered with a variety of beautiful things. There were necklaces made of starlight, rings encrusted with rare cosmic gems, and bracelets adorned with the power of the universe.\r\nSuddenly, Leviathan appeared before them, his yellow eyes glowing with an inner power. But instead of being afraid, the children felt happy and curious. 'Welcome to my penthouse,' Leviathan said in a deep, friendly voice. 'This is a place of magic and wonder, where I keep all of my favorite things. Look around and enjoy!' The children looked around the room, amazed by the beautiful and shiny things they saw. They realized that Leviathan was not just a powerful creature, he was also a symbol of beauty, style, and power.\r\nAnd so, the children left Leviathan's penthouse suite with a happy feeling in their hearts. They knew that they had seen something special, something that they would remember for a long, long time." },
                { "leviathan", "BOOK: Leviathan was a creature of legend, feared by all who knew of him. He lived in a penthouse suite, filled with objects that were said to come from hellish dimensions.\r\nThe walls of the suite were made of black obsidian, and the floors were polished to a high shine, reflecting the eerie glow of the artifacts and objects that filled the room.\r\nIn the center of the room was a massive throne made of twisted metal, adorned with dark jewels that seemed to pulse with an otherworldly power. Leviathan sat upon the throne, a hideous monster with yellow eyes that glowed with an inner malevolence.\r\nThe walls were lined with strange objects, each one more terrifying than the last. There were necklaces made of bones and teeth, rings that glowed with an otherworldly light, and bracelets that seemed to writhe and twist like living things.\r\nIn one corner of the room was a shimmering portal that seemed to lead to another dimension. Leviathan was rumored to use this portal to travel between worlds, wreaking havoc and destruction wherever he went.\r\nDespite the terror that permeated the room, Leviathan seemed to revel in his power and the fear he inspired. He would often invite guests to his suite, luring them in with the promise of wealth and power, only to trap them in his lair and feast upon their terror.\r\nThose who managed to escape Leviathan's grasp spoke of a room filled with horror and terror, a place where the boundaries between worlds were blurred and the forces of darkness held sway."},
                { "journal", "BOOK: ENTRY ONE: I've been searching the city for any sign of the artifact, but so far, I've come up empty-handed. I've scoured the markets, talked to traders and merchants, but no one seems to know anything.\r\n\r\nENTRY TWO: Today, I stumbled upon a magnificent statue of a dragon in the center of a fountain. Something about it caught my eye, and I couldn't help but feel that there was something significant about it.\r\n\r\nENTRY THREE: I spent hours examining the statue, but I couldn't find anything out of the ordinary. However, as I was about to give up, I noticed a faint groove where one of the dragon's eyes should have been. Could this be the clue I've been searching for?\r\n\rI've been searching the area around the statue for hours, but I haven't found anything that could be the missing eye. I'm starting to feel like it's a lost cause.\r\n\r\nENTRY FOUR: It's been days, and I still haven't found any sign of the missing dragon's eye. I'm starting to wonder if it even exists or if it's just a myth.\r\n\r\nI've decided to give up my search for the artifact. As much as I would love to uncover its secrets, it seems that it's just not meant to be. Perhaps it's for the best that some things remain hidden."},
                { "avatara", "BOOK: The brand's founders believed that the beauty of the Mana Elves was often overlooked in favor of their magical abilities, and they sought to change that. They wanted to create clothing that would celebrate the elegance and grace of the Mana Elves, while also incorporating the power and magic that they possessed.\r\n\r\nTo accomplish this, the founders of Avatara Couture combined their magical abilities with their expertise in tailoring and design. They used the finest materials, enchanted gems, and spell-imbued threads to create clothing that was not only beautiful but also imbued with magical properties.\r\n\r\nAs word of their creations spread, Avatara Couture quickly became a favorite among the magical elite. Its clothing was seen as a symbol of power and refinement, and its customers included wizards, witches, and other magical beings from all over the world, including the Xuolia Royal Guard and Royal Family.\r\n\r\nAt the heart of Avatara Couture is a philosophy that emphasizes the importance of beauty, elegance, and power. The brand's founders believed that clothing was not just a practical necessity but also a means of self-expression and empowerment.\r\n\r\nAvatara Couture is committed to using only the finest materials, including cosmic silk, dragon scales, and enchanted metals. The brand's designers meticulously tailor each piece of clothing to ensure a perfect fit, while also incorporating magical properties that enhance the wearer's abilities.\r\n\r\nThe philosophy of Avatara Couture is also reflected in its designs, which are characterized by their elegance, sophistication, and attention to detail. Each piece of clothing is a work of art, carefully crafted to showcase the unique beauty and magical abilities of the Mana Elves.\r\n\r\nAvatara Couture believes that fashion has the power to transform not just the way people look, but also the way they feel and the way they interact with the world around them. The brand is committed to creating clothing that not only looks beautiful but also imbues the wearer with a sense of confidence, power, and magic."},
                { "gold", "BOOK: Chapter 1: Understanding the Value of Gold\r\n\r\nAs an Aurizari, I have always been taught to appreciate the value of gold. It is not just a metal to us, but a sacred symbol of our connection to our god of wealth and prosperity. Our entire society is built around our ability to mine and refine gold, which we use to build our opulent cities and temples.\r\n\r\nGold grading is an important process that we Aurizari take very seriously. We have developed a sophisticated system for determining the quality and purity of gold, based on our deep knowledge of metallurgy and alchemy. However, not all forms of gold are created equal.\r\n\r\nChapter 2: The Purity of Gold\r\n\r\nThe purity of gold is measured in karats, with 24 karat gold being the purest form of gold. However, we also deal with a rare and dangerous form of gold called Black Gold. It is created by alloying gold with a metal known as blackmetal, which is found in deep underground veins. Black Gold has a unique black color and is incredibly valuable, but it is also dangerous to work with.\r\n\r\nBlackmetal is highly radioactive, and exposure to it can cause severe health problems. As a result, those who work with Black Gold must take extreme precautions and wear protective clothing and equipment.\r\n\r\nDetermining the purity of Black Gold requires an even greater level of expertise and caution. We use specialized equipment to measure the levels of radiation in the Black Gold, and only a select few who have undergone extensive training are allowed to handle it.\r\n\r\nChapter 3: The Color of Gold\r\n\r\nThe color of gold can also affect its value. We recognize four main colors of gold: yellow, white, rose, and green. Yellow gold is the most common and is often used in jewelry, while white gold is typically alloyed with other metals to create a silver-colored metal. Rose gold has a pinkish hue, while green gold has a greenish tint.\r\n\r\nBlack Gold is the only form of gold that is black in color. It is highly prized for its rarity and unique appearance, but its value is tempered by the dangers of working with it.\r\n\r\nChapter 4: The Texture of Gold\r\n\r\nThe texture of gold can also affect its value. We recognize two main textures of gold: soft and hard. Soft gold is more malleable and is often used in jewelry making, while hard gold is more durable and is often used in coin minting.\r\n\r\nBlack Gold is a hard form of gold, and its texture is incredibly smooth and polished. It is often used in the creation of high-end jewelry and luxury goods, but only those who are specially trained and equipped to handle it can work with it safely.\r\n\r\nChapter 5: Conclusion\r\n\r\nIn conclusion, the Aurizari have a deep understanding of gold and its many forms. While Black Gold is highly valued for its unique color and appearance, it is also dangerous to work with because of the radiation from the blackmetal used to create it. As a society, we are committed to maintaining our wealth and prosperity through the responsible mining and refining of all forms of gold, while also ensuring the safety of those who work with it."}
            },
            north: -1,
            south:-1,
            east: 31,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true
        ),
        new Room(
            name: "Tunnel to Xuolia",
            id: 12,
            description: "The walls and floor are composed of a shimmering, iridescent material that seems to refract and reflect light in a myriad of colors. As you make your way through the tunnel, you begin to notice a subtle change in the colors of the crystals. The iridescence gradually fades, replaced by a deep, rich blue that grows more intense with every step you take. Eventually, you realize that the crystals have given way entirely to a massive blue manastone that spans the entire width of the tunnel. Despite the shift in the tunnel's composition, you can still see perfectly thanks to the torches mounted on the walls. These torches are unlike any you've seen before, their flames a deep shade of purple that casts an eerie glow on everything around you.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 7,
            south:28,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true


        ),
        new Room(
            name: "S`Aquia Trade District",
            id: 13,
            description: "As you enter the trade district, you are immediately greeted by the lively sounds of bargaining and haggling. The air is filled with the aroma of spices and fresh seafood, and you can see a plethora of unique and exotic goods on display. You see vendors selling all sorts of items, from seashells and coral trinkets to rare spices and textiles. The streets are narrow and winding, and you can see fountains and small gardens scattered throughout. The district is crowded, with merchants and shoppers alike bustling about, their voices and laughter filling the air.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 10,
            south: 16,
            east: 14,
            west: 15,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Beachside Nook",
            id: 14,
            description: "As you make your way along the shoreline, you come across a small beachside nook tucked away behind a cluster of tall rocks. The sound of waves crashing against the rocks echoes through the air, creating a soothing melody that fills the space. You notice a group of siren women gathered in the nook, their voices carrying softly on the breeze. The nook itself is small, but beautiful. The sand is soft and golden, and the water is clear and warm. The rocks that surround the nook are covered in moss and seaweed, creating a natural barrier from the outside world. You can see small crabs and other sea creatures scurrying about on the rocks, adding to the sense of serenity and calm.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:-1,
            east: -1,
            west: 13,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Pavilion",
            id: 15,
            description: "The pavilion is made of gleaming white marble, with intricate carvings and statues decorating its walls and columns. It's an impressive sight, towering above the trees and flowers that surround it. The gardens themselves are a wonder to behold. A series of winding paths lead you through lush greenery, with colorful flowers and exotic plants lining the way. Small streams and ponds are scattered throughout, adding to the tranquility of the space. You can see butterflies and hummingbirds flitting about, drawn to the beauty of the gardens.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 17,
            east: 13,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Beachside Gardens Path",
            id: 16,
            description: "The path is surrounded by lush greenery, with tall blue cherry blossom trees and exotic plants lining the way. The air is filled with the sweet fragrance of blooming flowers, and you can hear the soothing sound of a nearby fountain. As you walk, you catch glimpses of the palace's exterior through the foliage, with white marble walls and intricately carved columns. The path itself is made of smooth white stones, and it winds gently uphill towards the palace gates.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 13,
            south:18,
            east: -1,
            west: 17,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Beachside Gardens",
            id: 17,
            description: "A series of winding paths lead you through lush greenery, with colorful flowers and exotic plants lining the way. Small streams and ponds are scattered throughout, adding to the tranquility of the space. You can see butterflies and hummingbirds flitting about, drawn to the beauty of the gardens. The garden's decorations are made of gleaming white marble, with intricate carvings, statues, and columns. High on a hill overlooking the gardens, you can see the grand water fountain and the Marble Palace. The palace itself is an impressive sight, towering above the trees and flowers that surround it. You can see the palace's exterior through the foliage, with white marble walls and intricately carved columns.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 15,
            south:-1,
            east: 16,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Hillside Path",
            id: 18,
            description: "The path winds its way up a steep hill towards the S`Aquia Palace, offering breathtaking views of the surrounding landscape. The path itself is rugged and uneven, carved into the hillside and surrounded by dense foliage and rocky terrain. As you ascend higher and higher up the hill, you can hear the sound of your footsteps echoing off the rocky walls and the rustling of leaves in the gentle breeze. Looking down, you can see the glimmering sea in the distance, while looking up you can catch glimpses of the palace's grand walls and ornate columns through the trees.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 16,
            south:-1,
            east: 21,
            west: 19,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Water Fountain",
            id: 19,
            description: "The fountain is a stunning work of art, with multiple tiers of cascading water and intricately carved marble sculptures of sea creatures and mermaids. The water sparkles in the sunlight, creating a beautiful display of light and color. The fountain's base is surrounded by lush greenery and colorful flowers, with blue cherry blossom trees and exotic plants lining the walkways. From the fountain's vantage point, you can see the beach gardens below the hill, with hanging gardens and small streams winding their way through the lush foliage. The sound of the waves crashing against the shore below is a constant reminder of the palace's proximity to the sea.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:20,
            east: 18,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Outside the Marble Palace",
            id: 20,
            description: "As you stand at the bottom of the grand staircase that leads up to the S`Aquia Palace, you're struck by the sheer magnificence of the structure. The palace is a stunning example of Siren architecture, with tall white columns and intricate carvings adorning its facade. The marble walls are smooth and gleaming in the sunlight, and the mosaics depicting sea creatures and ancient battles add to the palace's grandeur. Looking up, you can see the palace's towering walls and majestic archways, with ornate statues of sea creatures and mythical beasts perched atop the roof. The courtyard below is alive with the sounds of fountains and rustling leaves, with blue cherry blossom trees and colorful flowers adding to the sense of beauty and tranquility.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 19,
            south:-1,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Marble Pathway",
            id: 21,
            description: "The trees are taller and denser here, with their branches forming a natural canopy overhead that filters the sunlight and creates a dappled effect on the ground below. The path itself is smooth and polished, making it easy to walk on, and it's flanked on either side by lush gardens bursting with colorful flowers and exotic plants. You can hear the sound of birds singing in the trees, and the sweet fragrance of blooming flowers fills the air. As you continue down the path, you catch glimpses of the nearby residential district, with its colorful rooftops and winding streets.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 22,
            south:26,
            east: 24,
            west: 18,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "Lush Path",
            id: 22,
            description: "The path is lined with tall blue cherry blossom trees, their branches creating a natural canopy overhead. The air is filled with the sweet fragrance of blooming flowers, and you can hear the gentle sound of water trickling from nearby fountains. The path is smooth and well-maintained, making it easy to walk on, and it gently slopes upward toward the hanging gardens. You can see the vibrant colors of the gardens ahead, with a riot of colorful flowers and exotic plants hanging from the walls and archways. The sound of birds and insects fills the air, and you can feel a sense of peace and tranquility in this beautiful natural setting.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:21,
            east: 23,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Hanging Gardens",
            id: 23,
            description: "The gardens are a true marvel, with colorful flowers and exotic plants hanging from every wall and archway. The air is filled with the sweet fragrance of blooming flowers, and you can hear the gentle sound of water trickling from nearby fountains. The gardens are a riot of color and texture, with vibrant hues of red, purple, and yellow blending together in a stunning display.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:21,
            east: -1,
            west: 22,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Residential District",
            id: 24,
            description: "The district is a maze of winding streets and alleys, with colorful rooftops and ornate balconies adorning the buildings. The houses are designed to be open and airy, with large windows and balconies that overlook the sea. The buildings are adorned with intricate carvings of sea creatures and mermaids, reflecting the siren population's connection to the sea. The streets are narrow and winding, creating a sense of intimacy and coziness, and are made from smooth and polished stone that's easy on the feet of the siren's thralls. You can see small gardens and fountains scattered throughout the district, with exotic plants and sea creatures featured prominently. As you walk through the streets, you can catch glimpses of the nearby sea, with its blue waters sparkling in the sunlight. The district is bustling with life, with sirens and their thralls going about their daily business. The sounds of singing and music fill the air, creating a warm and welcoming atmosphere.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:-1,
            east: 25,
            west: 21,
            canRecall: false,
            forageableItems: seasideForagableItems

        ),
        new Room(
            name: "S`Aquia Chapel",
            id: 25,
            description: "The chapel is a small and intimate space, with tall marble columns and intricate carvings adorning the walls. The floors are made of smooth and polished stone, and the air is filled with the woody fragrance of driftwood incense. The chapel is dimly lit, with soft candlelight casting a warm and inviting glow throughout the space. The altar is situated at the front of the chapel, with an ornate statue of a siren prominently displayed. The statue is made of shimmering white marble, with intricate carvings that reflect the siren's beauty and power. The chapel is adorned with paintings and tapestries depicting the sea and mythical creatures, adding to the sense of wonder and enchantment.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south:-1,
            east: -1,
            west: 24,
            canRecall: true,
            forageableItems: noForagableItems,
            indoor: true


        ),
        new Room(
            name: "Blue Cherry Grove",
            id: 26,
            description: "The grove is a dense and lush area, with tall blue cherry blossom trees towering overhead. The trees form a natural canopy, creating a sense of intimacy and privacy within the grove. The air is filled with the sweet fragrance of blooming flowers, and the ground is covered in a soft carpet of fallen petals. As you walk through the grove, you can see small streams and ponds scattered throughout, creating a sense of tranquility and peace. The grove is alive with the sounds of birds singing and insects buzzing, adding to the sense of natural wonder. As you continue walking, you come across a small clearing in the grove, where you can see a series of hammocks strung up between the trees. The hammocks are made from soft and comfortable fabric, and are the perfect place to rest and relax.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 21,
            south:-1,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems

        ),
        new Room(
            name: "The Plane of Dream",
            id: 27,
            description: "As you awaken in the Plane of Dream, you feel a sense of disorientation wash over you. You look around, taking in your surroundings, and realize that you are standing outside a magnificent pink \x1b[38;2;255;105;180mcastle\x1b[0m that seems to be shimmering in the light. The castle is unlike any structure you've seen before, with \x1b[38;2;255;105;180mturrets\x1b[0m that reach up to the clouds and walls that seem to be made of some sort of crystalline material. As you stand there, trying to make sense of your surroundings, you notice a small blue \x1b[96mimp\x1b[0m standing just a few feet away, watching you intently. The air around you is thick with the scent of flowers and the sound of distant music, and you feel a sense of magic and wonder emanating from everything around you.\r\n\x1b[92mHINT: You can 'inspect <object name>' certain things in the room to learn more about them.\x1b[0m\r\n\x1b[92mHINT: You can type 'help' to see a list of all available commands.\x1b[0m",
            objectsInRoom: new Dictionary<string, string>
            {
                { "turrets", "As you look closer, you can see that each turret is adorned with intricate carvings and delicate filigree work, adding to their already impressive appearance. Some of the carvings depict fantastical creatures, while others seem to depict scenes from ancient myths and legends. You also notice that each turret has a different shape and size, adding to the castle's already eclectic appearance. Some are tall and slender, while others are shorter and wider, giving the castle a sense of whimsy and playfulness." },
                { "castle", "The walls seem to be made of a translucent, almost glass-like material that catches the light and reflects it back in dazzling patterns. The surface of the walls is covered in intricate patterns, etched into the material with astonishing precision."},
                { "imp", "Its skin is a vivid shade of blue, and it shimmers slightly in the sunlight, giving it an otherworldly appearance. The imp is about three feet tall and has a slim, agile build. Its wings are a darker shade of blue, and they seem to be constantly fluttering, as if the imp can barely contain its excitement. Its face is angular and sharp, with pointed ears that stick out from the top of its head. Its eyes are a bright yellow, and they seem to sparkle with an inner intelligence.\r\n\x1b[92mHINT: You can 'talk <entity name>' to certain entities to interact with them.\x1b[0m"}
            },
            north: -1,
            south:-1,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems

        ),
        new Room(
            name: "Xuolia Foyer",
            id: 28,
            description: "The foyer area of the city of Xuolia is a grand chamber that serves as the entrance to the underground city. The chamber is vast, with towering ceilings that seem to stretch up into the darkness above. The manastone walls are lined with intricate carvings and glowing runes that pulse with magical energy, casting an ethereal light throughout the chamber.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 31,
            south: 12,
            east: 29,
            west: 30,
            canRecall: true,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Xuolia Visitor's Center",
            id: 29,
            description: "As you enter the Xuolia Visitor's Center, you find yourself standing in a grand entry hall that exudes an air of elegance and sophistication. The walls are crafted from polished black obsidian, which seems to shimmer and reflect the dim light of the magical lamps hanging from the ceiling. The floor beneath your feet is made of smooth, white marble, which contrasts beautifully with the dark walls. In the center of the foyer, there is a large circular desk made of sparkling crystal. Behind the desk, a tall, regal-looking Mana Elf with piercing blue eyes greets you warmly. She wears a flowing robe of shimmering purple fabric and carries a wand of twisted wood, adorned with intricate arcane symbols. To your left and right, there are several alcoves containing displays of beautiful, intricate artifacts crafted by the skilled artisans of Xuolia. There are glittering crystal sculptures, delicate jewelry adorned with glowing gemstones, and ornate tapestries depicting scenes of magical wonder.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: -1,
            west: 28,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Xuolia Chapel",
            id: 30,
            description: "The room is illuminated by dozens of softly glowing orbs that hang from the high ceiling, casting a warm and inviting light over everything. The walls of the chapel are made of a shimmering, iridescent material that seems to shift and change in color depending on the angle of the light. The effect is mesmerizing, and you find yourself staring at the walls, almost hypnotized by their beauty. In the center of the room is an altar made of gleaming white marble, carved with intricate designs and runes that pulse with magical energy. The altar is surrounded by a circle of ornate silver candelabras, which burn with a soft, flickering flame. It is a place where the Mana Elves come to commune with the spirits of magic and to seek guidance and wisdom in their lives.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 28,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Xuolia Mana Fountain",
            id: 31,
            description: "As you enter the cavern, you are immediately struck by the vibrant glow emanating from the fountain at the center of the room. The walls of the chamber are lined with pulsing crystals, each one imbued with a different hue of magical energy. The fountain itself is a towering structure, nearly 20 feet in height, made of a shimmering, crystalline substance that seems to shift and change colors as you move around it. The fountain is fed by a series of underground streams, each one channeling magical energy from deep within the earth. The chamber is dimly lit, with glowing crystals providing the only source of illumination. Despite the dim lighting, the chamber feels alive with magical energy, and you can sense the power flowing through every corner of the room. The sound of trickling water can be heard echoing through the chamber, as the streams that feed the fountain continue to flow endlessly, providing a steady stream of magical energy to the city of Xuolia.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 32,
            south: 28,
            east: -1,
            west: 11,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Xuolia Marketplace",
            id: 32,
            description: "The market is a vast, open space, lit by glowing crystals and lined with stalls and shops selling all manner of exotic goods and magical artifacts. The air is thick with the scent of burning incense and exotic spices, and the sounds of haggling merchants and bargaining customers fill the air. Despite the constant hustle and bustle of the market, there is a sense of order and organization to the chaos. Trained guards patrol the perimeter of the market, ensuring the safety of the merchants and their wares, and a group of scribes keep detailed records of all transactions, making sure that everything is above board and that no one is taken advantage of.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 35,
            south: 31,
            east: -1,
            west: 33,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Xuolia Dragon Statue",
            id: 33,
            description: "To the west of the Xuolia Marketplace lies a grand chamber, its walls lined with glittering crystals that cast a soft, ethereal light. In the center of the chamber stands a magnificent statue of a dragon, its scales carved from smooth, shimmering marble that seems to change color as the light hits it from different angles. The dragon is depicted in a dynamic pose, its wings spread wide as if about to take flight. Its eyes, carved from glowing purple gems, seem to follow visitors as they move through the chamber.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 32,
            west: 34,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Afterdark Lounge",
            id: 34,
            description: "The interior of the Afterdark Lounge in Xuolia exudes an atmosphere of relaxation and indulgence. The walls are draped in deep shades of purple and black, giving the room a cozy, intimate feeling. The space is dimly lit by flickering candles, casting a warm glow over the patrons and the furnishings. The lounge is furnished with plush sofas and chairs, arranged in small groups throughout the room. Each seating area is separated by ornate wooden screens, providing a sense of privacy and seclusion. The furniture is upholstered in rich, luxurious fabrics, and accented with metallic trim and intricate embroidery. In one corner of the room, a small stage is set up for performances by musicians and other entertainers. The stage is framed by heavy velvet curtains, and a soft spotlight illuminates the area, drawing attention to the performers. The bar dominates the center of the room, and is constructed of gleaming polished wood. Bottles of exotic liquors and potions line the shelves behind the bar, their labels decorated with intricate calligraphy and exotic symbols. The air is thick with the heady scent of magical smokes and exotic perfumes, creating a dreamlike atmosphere that encourages patrons to let go of their worries and indulge in the pleasures of the night.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 33,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            alternativeExits: new Dictionary<string, int>
            {
                { "down", 51 }
            },
            indoor: true

        ),
        new Room(
            name: "Xuolia Waterfront",
            id: 35,
            description: "The Xuolia Waterfront is a serene and tranquil area located at the edge of an expansive underground lake that stretches as far as the eye can see. The air is cool and damp, and the sound of lapping water echoes off the cavern walls. The area is illuminated by glowing orbs that float above the water, casting a gentle blue light across the scene. In the distance, there is a towering waterfall that cascades down from above and crashes into the lake with a deafening roar. The mist from the waterfall creates a fine spray that drifts across the waterfront, leaving everything feeling damp and refreshed.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 32,
            east: 36,
            west: 37,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Strange Door",
            id: 36,
            description: "To the east of the Xuolia waterfront, there is a narrow passage that leads to a small chamber carved out of the shimmering manastone wall. At the end of the chamber, there is a large, ornate door, unlike any other in the city. The door is made of a strange, metallic material, with intricate, glowing runes etched into its surface.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: -1,
            west: 35,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Outside Club Crimson",
            id: 37,
            description: "To the west of the Xuolia waterfront, you come across an ornate building made of shimmering crimson stone. This is Club Crimson, an exclusive members-only club for the most elite in Xuolia. The exterior of the building is designed to exude luxury and sophistication, with intricate carvings and embellishments adorning every surface. The entrance to the club is marked by a large, wooden door with an intricate, silver handle. The door is flanked by two imposing, stone statues of Mana Elves, their eyes seemingly following your every move. Above the entrance, a sign in flowing, golden letters reads 'Club Crimson.' The building itself is several stories tall, with large, arched windows lining the upper floors. The windows are stained glass, depicting scenes of magical battles, mythical creatures, and other fantastical imagery. The roof is adorned with ornate spires and gilded decorations, adding to the grandeur of the structure. As you approach, you can hear the faint sound of music and laughter emanating from within, giving you a sense of the revelry and exclusivity that awaits inside. A grumpy looking Mana Elf guard stands at the entrance, his arms crossed and his eyes narrowed.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 35,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Outside the Drybone Cove Portal",
            id: 38,
            description: "Directly in front of the portal to Drybone Cove, there is a small pedestal made of a dark, obsidian-like material. On top of the pedestal, there is a small, glowing orb that seems to be the source of the portal's power. The orb emits a steady, pulsing hum that fills the air, and the surrounding crystals seem to vibrate in response to its energy.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 7,
            west: -1,
            canRecall: true,
            forageableItems: noForagableItems,
            permanentPortal: 39

        ),
        new Room(
            name: "Drybone Docks",
            id: 39,
            description: "The wooden dock stretches out over the crystal-clear waters, lined with barrels and crates of goods waiting to be loaded onto the ships. The salty sea breeze fills the air, and the sound of seagulls can be heard overhead. At the end of the dock, a lighthouse towers above the other buildings, casting its light out over the dark waters to guide ships safely into the cove. An airship dock sits at half the height of the lighthouse.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 42,
            south: -1,
            east: 40,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            permanentPortal: 38,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Lighthouse",
            id: 40,
            description: "The Drybone Lighthouse stands tall and proud, overlooking the rocky coastline of the cove. The lighthouse itself is a towering structure, with a spiraling staircase leading to the top where the powerful light beam is emitted into the darkness of the night. At the base of the lighthouse, there is a small dock where airships can moor. The dock is made of sturdy wood, with several ropes and chains tied to large boulders to keep the airships secure. The dock is only half the height of the lighthouse, so airship passengers have to disembark using a set of stairs leading down to the dock. The area around the lighthouse is rocky and uneven, with several large boulders jutting out of the ground. The air is salty and briny, with a constant breeze blowing in from the sea. Seagulls wheel and cry overhead, their white wings flashing in the sunlight.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 41,
            south: -1,
            east: -1,
            west: 39,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Airship Dock",
            id: 41,
            description: "As you stand on the airship dock at the Drybone Lighthouse, you feel the wind gently tugging at your clothes and the wooden planks beneath your feet creaking slightly. Looking to the east, you see the massive airship, its hull made of gleaming metal and adorned with intricate golden detailing. The airship is enormous, easily the size of a small building, and it looms above you like a great metallic beast. Its three propellers spin lazily in the breeze, their blades glinting in the sunlight. A gangplank stretches from the dock up to the airship's boarding ramp, inviting you to step aboard and embark on your journey to New Mechatropolis.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 40,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades

        ),
        new Room(
            name: "Drybone Cove",
            id: 42,
            description: "The sun beats down on your skin, creating a warm contrast to the water's chill. To the south, the wooden docks extend out into the cove, bobbing gently in the waves. You can hear the sound of seagulls cawing in the distance, as they circle overhead. You take a moment to relax, floating on your back and gazing up at the clear blue sky. The sounds of the waves gently lapping against the shore and the distant cries of the gulls are the only noises you hear. As you close your eyes and float in the peaceful water, you feel a deep sense of relaxation and contentment.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 43,
            south: 39,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Cove",
            id: 43,
            description: "To the south, a labyrinth of wooden docks juts out into the sea, forming a maze of intersecting paths and towering pilings that rise from the depths like skeletal fingers. The scent of salt and seaweed is heavy in the air, mingling with the tang of brine and the distant cry of gulls. To the north, a strip of pristine sand stretches as far as the eye can see, gleaming white in the bright sunlight. Waves lap gently at the shore, leaving behind tiny bubbles that dance and shimmer in the light. The whole world seems to be alive with the pulsing rhythm of the ocean, a wild and untamed force that can sweep you away in an instant if you're not careful.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 44,
            south: 42,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Cove",
            id: 44,
            description: "You find yourself immersed in the cool, refreshing waters of Drybone Cove, surrounded by a vast expanse of crystal-clear, aquamarine sea. Looking towards the southern horizon, you can see a maze of wooden docks and piers stretching out towards the open ocean. To the north, a pristine beach of sparkling white sand awaits, lined with swaying nectarnut trees. The bright sun overhead casts a warm, inviting glow over the entire scene, and a gentle breeze carries the sound of waves lapping against the shore and the scent of saltwater on the air.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 45,
            south: 43,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Beach",
            id: 45,
            description: "The sand is a coarse, grayish-brown, flecked with bits of rusted metal and broken glass that glint in the weak sunlight. The air is thick with the acrid smell of oil and machine grease, mingling with the salty tang of the sea.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 44,
            east: 46,
            west: -1,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Beach",
            id: 46,
            description: "Along the beach, abandoned machinery and discarded robotics lie strewn about, their metal frames twisted and rusted by the constant pounding of the waves. Broken ship parts jut out of the sand like jagged teeth, while old cargo containers and shipping crates lie half-buried in the sand, their once-bright colors faded and peeling in the harsh salt air.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 50,
            south: -1,
            east: 47,
            west: 45,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Beach",
            id: 47,
            description: "The sea breeze whips through your hair, and the sound of waves crashing against the shore echoes in the distance. The sun beats down on the landscape, casting long shadows over the abandoned machinery, now taken over by rust and time. The remnants of robotic parts lay broken and useless, no longer serving the purposes they were created for.",
            objectsInRoom: new Dictionary<string, string>(),
            north: 49,
            south: -1,
            east: 48,
            west: 46,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
            
        ),
        new Room(
            name: "Drybone Beach",
            id: 48,
            description: "The sand beneath your feet is gritty and coarse, and as you look around, you see discarded machinery and ship parts scattered haphazardly along the shoreline. The air is thick with the scent of salt and rust, and the sound of waves crashing against the shore echoes in your ears.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: -1,
            west: 47,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Drybone Beach",
            id: 49,
            description: "Discarded machinery and giant robots of all shapes and sizes lie haphazardly among the debris, rusting and forgotten. The salty sea air fills your lungs, and the sound of the waves crashing onto the shore is almost deafening. The sun beats down upon your skin, casting a warm glow over the scene before you. In the distance, seagulls can be heard cawing as they soar above the beach. The atmosphere is a mix of both serenity and desolation, as if the beach is a place of both wonder and abandonment.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: 47,
            east: -1,
            west: 50,
            canRecall: false,
            forageableItems: seasideForagableItems,
            randomRoomMessages: seasideSounds,
            randomRoomTimer: randomRoomTimer,
            randomRoomColors: lightBlueShades
        ),
        new Room(
        name: "Drybone Beach",
        id: 50,
        description: "The hulking frames of giant robots lie scattered like fallen giants, their limbs twisted and broken. The rusted hulls of old ships jut out from the sand, their splintered masts rising towards the sky. The air is thick with the scent of salt and oil, and the sound of waves crashing against the shore provides a steady backdrop to the eerie stillness of the abandoned machines.",
        objectsInRoom: new Dictionary<string, string>(),
        north: -1,
        south: 46,
        east: 49,
        west: -1,
        canRecall: false,
        forageableItems: seasideForagableItems,
        randomRoomMessages: seasideSounds,
        randomRoomTimer: randomRoomTimer,
        randomRoomColors: lightBlueShades
        ),
        new Room(
            name: "Afterdark Lounge Basement",
            id: 51,
            description: "Descending the stairs to the basement of the Afterdark Lounge, the atmosphere changes from cozy and intimate to seedy and secretive. The air is heavy with smoke, making it difficult to see beyond a few feet. The dim lighting from flickering neon orbs and dim candles hanging from the ceiling casts eerie shadows on the walls. Exotic dancers move about on a small stage, gyrating to the music and captivating the audience with their sensual movements. The stage is surrounded by a group of men, some cheering and throwing money while others watch in quiet contemplation. The patrons are a motley crew of strange and unusual characters, each with their own unique story and hidden secrets. At the far end of the room is a private VIP area, accessible only to those with the right connections or enough money to bribe their way in. It's a small enclave, shrouded in secrecy, with plush red velvet sofas and low lighting, creating an intimate and seductive atmosphere.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 53,
            west: 52,
            canRecall: false,
            forageableItems: noForagableItems,
            alternativeExits: new Dictionary<string, int>
            {
                { "up", 34 }
            },
            indoor: true

        ),
        new Room(
            name: "Seedy Room",
            id: 52,
            description: "As you make your way through the smoky and dimly lit basement of the Afterdark Lounge, you come across a small, unmarked door on the west side of the room. The door is old and weathered, with peeling paint and rusted hinges, giving it an eerie and foreboding appearance. As you push the door open, you find yourself in a small, cramped room with barely enough space to stand. The walls are made of rough stone, and the air is thick with the musty smell of dampness and decay. In the center of the room, there's a single wooden table with a dimly lit candle placed on top, casting long shadows on the walls. Sitting behind the table is a robed figure with their face obscured by a hood, their features shrouded in darkness. The walls are lined with bookshelves, filled with dusty tomes and ancient grimoires, their titles written in archaic languages and strange symbols. The shelves are littered with strange artifacts and talismans, hinting at the dark and arcane knowledge that is being passed on in this room.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: 51,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Afterdark Lounge VIP Area",
            id: 53,
            description: "The lighting is low, with a soft, warm glow emanating from crystal chandeliers hanging from the ceiling. The walls are draped in luxurious fabrics, with deep shades of red and gold creating an opulent atmosphere. The furniture is plush and comfortable, with large leather sofas and armchairs arranged in small groups around low coffee tables. The furnishings are adorned with intricate embroidery, and the occasional gleaming metallic accent adds to the air of extravagance. The bar in the VIP area is fully stocked with top-shelf liquors and wines, with bottles displayed on mirrored shelves that reflect the dim lighting. Bartenders dressed in formal attire are quick to serve, and the drinks are expertly mixed. The VIP area is a place where power, wealth, and privilege meet in a heady mix of luxury and decadence. It's a world away from the seedy underbelly of the basement, catering to those who can afford the price of admission.",
            objectsInRoom: new Dictionary<string, string>(),
            north: -1,
            south: -1,
            east: -1,
            west: 51,
            canRecall: false,
            forageableItems: noForagableItems,
            keyItem: "Afterdark VIP Key",
            indoor: true

        ),
        new Room(
            name: "Dreamweaver's Hall",
            id: 54,
            description: "The Dreamweaver's Hall is an awe-inspiring chamber with high, vaulted ceilings adorned with intricate \x1b[96mfresco\x1b[0m paintings depicting scenes from Magnos's history. The walls are lined with tall, arched windows that bathe the room in a soft, ethereal glow. Majestic columns stand tall throughout the hall, each carved with the likeness of mythical creatures and legendary heroes. In the center of the hall stands a magnificent crystal chandelier, its delicate tendrils of light casting mesmerizing patterns on the polished marble floor below. A grand, sweeping staircase leads to a raised platform where a massive, ornate throne sits, seemingly untouched by time. The throne is flanked by two statues of hooded figures, their identities a mystery. Whispers of ancient dreams and memories seem to linger in the air, imbuing the hall with an atmosphere of reverence and mystique. An important-looking \x1b[92mtome\x1b[0m sits on a pedestal in the center of the room, its pages filled with strange symbols and arcane knowledge.",
            objectsInRoom: new Dictionary<string, string>()
            { 
                { "fresco", "A breathtaking fresco painting unfolds across the vast expanse of the ceiling, capturing the divine creation and tragic downfall of Magnos. At the center of the scene, the god of creation, Zerjz, is portrayed in all his magnificence. He stands tall and regal, with flowing robes that seem to merge with the celestial background, giving the impression that he is woven from the very fabric of the cosmos. His outstretched hands radiate with divine power, as he breathes life into Magnos, which materializes as a vibrant, swirling mass of color and light below him.\r\nThe fresco then transitions to a darker, more ominous scene. Sharaa, the false being of light, is depicted as an alluring yet sinister figure, with radiant wings that cast deceptive shadows. Her face is a haunting blend of beauty and malice, as she reaches out with a gleaming dagger, plunging it into the unsuspecting Zerjz's back. The contrast between the bright aura of Sharaa and the darkness in her eyes conveys the duality of her nature and the treachery of her actions.\r\nIn the final scene of the fresco, the lifeless body of Zerjz floats in the cold void of space, surrounded by a somber tapestry of stars and celestial bodies. His once brilliant eyes now appear dull and lifeless, while the divine energy that once emanated from his being has been extinguished. The painting serves as a poignant reminder of the fragility of creation and the destructive power of deceit, immortalizing the tragedy of Magnos's divine origin in the heart of the Dreamweaver's Hall." },
                { "tome", "BOOK: The tome reads, 'Traveler, your journey through Magnos will be fraught with challenges and wonders alike. To aid you on your path, we have documented essential commands that will empower you to interact with this world. To access a comprehensive list of commands, simply type 'help' at any time. This will display a menu detailing various actions you can perform and provide a brief explanation of their functions. We encourage you to experiment with these commands, as they will allow you to interact with the environment, communicate with other beings, and unlock the secrets of Magnos. Do not hesitate to use them as you explore the Dream Castle and beyond. Your curiosity and resourcefulness will be your greatest allies in this adventure. Remember, traveler: the key to mastering the realms of Magnos lies in understanding the tools at your disposal. Be bold, seek knowledge, and let the commands guide you on your path'"},
                },
            north: 57,
            south: 27,
            east: 56,
            west: 55,
            canRecall: false,
            forageableItems: noForagableItems,
            keyItem: "Dream Castle Key",
            timedMessage: "\x1b[92mHINT: Remember to use 'inspect <object name>' to look more closely at things in the room. This will reveal additional information about the object.\x1b[0m",
            messageDelay: 10,
            indoor: true

        ),
                new Room(
            name: "Chamber of Memories",
            id: 55,
            description: "The Chamber of Memories is a breathtaking, circular room with walls made entirely of shimmering, translucent crystal. Each crystal pane is etched with intricate patterns, forming a mesmerizing mosaic that spans the entire chamber. The floor is a smooth, polished marble, reflecting the soft, ambient glow that emanates from the crystalline walls. The Chamber of Memories is adorned with numerous glowing \x1b[96morbs\x1b[0m that hang gracefully from the high ceiling. Suspended by delicate, silver chains, these orbs appear to be crafted from a mysterious, translucent material that softly diffuses the light within them, casting a warm, soothing glow throughout the chamber. Each orb pulsates with a unique rhythm, reminiscent of a heartbeat, as if they hold the living memories of Magnos within their ethereal depths. The orbs seem to represent different epochs, events, or entities from the realm's history, their colors and intensities varying to reflect the emotions and significance of the memories they contain. As you move through the chamber, the orbs gently sway, creating a mesmerizing dance of light and shadow. The gentle illumination they provide enhances the crystal panes' vivid scenes, adding depth and richness to the memories on display.",
            objectsInRoom: new Dictionary<string, string>()
            { 
                { "orbs", "The orbs are suspended from the ceiling by slender, silvery chains that seem to sparkle in the dim light. The chains are attached to ornate fixtures, each one adorned with various gemstones that complement the colors of the orbs themselves. Despite their fragile appearance, the orbs feel solid and cool to the touch, with a smooth, glass-like texture. As you stand in the chamber, the orbs emit a faint hum of energy, suggesting that they contain some sort of magical essence or power within them. This hum is punctuated by fragmented messages that fill the room with sound, bouncing off the walls." },
                },
            north: -1,
            south: -1,
            east: 54,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            randomMessages: new List<string> { "\x1b[92mIn the... beginning... Magnos... pristine... creatures... landscapes... strange... flickering... lights... sky...\x1b[0m", "\x1b[94mMagic... trick?... fear... terrible... happening... strange beings... demons... destroy... world...\x1b[0m", "\x1b[93mYears... passed... demons... visit... Magnos... never harm... studying... information... secrets...\x1b[0m", "\x1b[91mInhabitants... understand... demons... humans... parallel universe... machines... technology... interact... Magnos...\x1b[0m", "\x1b[96mSuspicion... fear... outsiders... conquer... steal secrets... interact... Magnos... learn... magic... mysteries...\x1b[0m", "\x1b[38;2;120;81;169mOver... time... humans... inhabitants... work... together... knowledge... discoveries... technology... magic... universe...\x1b[0m", "\x1b[93mToday... Magnos... humans... strange... mysterious... allies... friends... new knowledge... understanding... world...\x1b[0m", "\x1b[91mDespite... visiting... Magnos... digital avatars... suspicion... fear... outsiders... invading... wary... intentions... demon... slur... fears... suspicions... work together... better future...\x1b[0m" },
            randomMessagesDelay: 8,
            indoor: true

        ),
                                new Room(
            name: "Archive of Tomes",
            id: 56,
            description: "The Archive of Tomes is an awe-inspiring room filled with towering bookshelves that stretch from floor to ceiling. The walls are lined with ancient, intricately carved wooden shelves, each one housing countless volumes of knowledge. The \x1b[93mbooks\x1b[0m themselves range from small, leather-bound tomes to massive, ornate volumes adorned with gold leaf and precious stones. The scent of aged parchment and ink fills the air, creating an atmosphere of reverence and wisdom. Intricate ladders made of polished mahogany are positioned along the bookshelves, providing access to the highest volumes. Each ladder is connected to a clever system of rails, allowing it to glide effortlessly along the shelves as needed. The ladders seem to possess a faint magical aura, suggesting that they may have been enchanted to move in response to a visitor's needs. In the center of the room, several large, ornately carved wooden tables are arranged, providing ample space for visitors to peruse the tomes and conduct research. Plush, high-backed chairs upholstered in rich velvet surround the tables, inviting hours of comfortable study.",
            objectsInRoom: new Dictionary<string, string>()
            { 
                { "books", "A few nearby titles catch your attention: The Frozen \x1b[94mPurgatory\x1b[0m, \x1b[92mAstral\x1b[0m Link, On \x1b[93mGorgons\x1b[0m" },
                { "purgatory", "BOOK: The tale of the Frozen Purgatory is one of triumph and sacrifice, detailing the final moments of the cosmic sorcerer Black Star and the creation of a holding chamber for new refugees to Magnos. This chronicle pays homage to Black Star's last act and the moon elf hero who played a crucial role in its creation.\r\n\r\nBlack Star was a formidable cosmic sorcerer who held immense power over the elements and the fabric of reality itself. Throughout his life, he sought to unravel the secrets of the cosmos and harness them for his own purposes. His mastery of the arcane arts allowed him to bend time and space to his will, but his ambition for power led him down a dark path.\r\n\r\nA brave and skilled moon elf, renowned for their wisdom and combat prowess, emerged as the champion of Magnos. The moon elf hero led the charge against Black Star, determined to put an end to his sinister deeds and protect the world from his malevolent influence.\r\n\r\nIn an epic clash of magic and might, the moon elf hero confronted Black Star, and a fierce battle ensued. The two combatants pushed each other to their limits, their spells and attacks creating a dazzling spectacle that shook the heavens.\r\n\r\nIn the final moments of the battle, the moon elf hero's determination and righteous cause prevailed. Defeated and enraged, Black Star sought to exact revenge on the world that had rejected him. With the last remnants of his power, he began to weave an intricate spell to create a cursed holding chamber for newcomers to Magnos - a place that would torment them and serve as a reminder of the dark legacy he would leave behind.\r\n\r\nThe spell Black Star cast gave birth to the Frozen Purgatory, a sinister realm suspended between time and space, shrouded in a perpetual frost. This realm ensnared the souls of newcomers to Magnos, trapping them within its icy walls and subjecting them to the unbearable cold and isolation. The Frozen Purgatory stood as a monument to Black Star's malevolence, a twisted reflection of his hatred and desire for vengeance against the world that had vanquished him.\r\n\r\nThe Frozen Purgatory stands as a testament to the cosmic sorcerer Black Star's descent into darkness and the moon elf hero's unwavering spirit. It serves as a reminder of the consequences of unchecked ambition and the importance of resisting the allure of vengeance. For those who find themselves trapped within its icy walls, the Frozen Purgatory offers no solace or sanctuary, but rather a chilling reminder of the darkness that can lurk within the hearts of even the most powerful beings." },
                { "gorgons", "BOOK: In the heart of the cosmos, on a world shrouded in darkness and mystery, lies the Gorgon's homeland. This place is a realm where the Gorgons thrive and rule with an iron fist. The landscape is harsh and unforgiving, with jagged peaks and treacherous ravines dominating the terrain. Yet, amidst this desolation, the Gorgons have built a formidable civilization.\r\n\r\nThe Gorgons worship a pantheon of dark gods and goddesses, each representing a different aspect of their society and culture. These deities are both feared and revered, and the Gorgons believe that their favor can grant them great power and fortune. At the center of their religion is Leviathan, the Gorgon man who rose to become the architect of the cosmos of Magnos. He is seen as the embodiment of power and wisdom and is often depicted as a serpent coiled around the cosmos, guarding the secrets of creation.\r\n\r\nThe Gorgons are master artisans, creating works of art that are both beautiful and terrifying. Their sculptures and paintings often depict scenes of battle and conquest, as well as the dark gods they worship. The craftsmanship of these works is unparalleled, showcasing the Gorgons' skill and attention to detail.\r\n\r\nThroughout the cosmos, the Gorgons are both feared and respected. Their dark deeds and malevolent nature have earned them a place among the most powerful beings in existence. Yet, for all their cruelty and ambition, the Gorgons have also brought about progress and innovation.\r\n\r\nTheir pursuit of knowledge and power has led them to explore the depths of the universe, uncovering secrets that have shaped the course of history. They have left an indelible mark on the cosmos, their influence felt in every corner of existence." },
                { "astral", "BOOK: In the bustling city of Mechatropolis, Dizian Maltor, an Arcturian sector leader, was hard at work in his lab. Dizian was passionate about travel and expanding the potential of Magnos as a travel hub. He had a vision: a device that would revolutionize the way people traversed the cosmos - the Astral Link.\r\n\r\nOne fateful day, a wandering sage named Eltharin arrived in Mechatropolis. Recognizing the potential for collaboration, Dizian invited the sage to join him in his quest to create the Astral Link. Eltharin, an expert in the manipulation of cosmic energies, embarked on a journey with Dizian that would take them to the farthest reaches of Magnos in search of the key to unlocking the Astral Link's potential.\r\n\r\nOn the distant moon of Zalthor, they discovered an ancient artifact known as the Eye of the Cosmos. The Eye of the Cosmos was said to contain the power to bend time and space. As they studied it, Dizian and Eltharin realized that it held the key to completing the Astral Link. They worked tirelessly, incorporating the artifact's energy into their device. After countless trials and errors, Dizian and Eltharin succeeded in creating a stable Astral Link. The device revolutionized travel, allowing users to travel instantaneously across the cosmos. Trade routes expanded, people explored previously unreachable territories, and new alliances were formed. Magnos researchers began sending exploratory signals to various corners of the universe, including Earth.\r\n\r\nA computer programmer named Daniel Thompson discovered the messages and unwittingly became an integral part of a grand experiment. Following the instructions, Daniel created a unique piece of software designed to facilitate the first-ever Astral Link between Earth and Magnos. With the completion of the software, Daniel initiated the program and found himself transported to Magnos.\r\n\r\nDaniel's arrival marked the beginning of a new era of cooperation and discovery between Earth and Magnos. The Astral Link became a symbol of unity between the two worlds, forever changing the course of history for both and paving the way for further interstellar exploration." },

            },
            north: -1,
            south: -1,
            east: -1,
            west: 54,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
                                new Room(
            name: "Nexus Portal Chamber",
            id: 57,
            description: "The chamber itself is a large, circular room with a soaring, domed ceiling that is adorned with intricate murals depicting the cosmos and the many worlds that lie within it. Bioluminescent crystals embedded in the walls and ceiling provide a soft, otherworldly glow that illuminates the chamber in shades of blues, greens, and purples. The floor of the Nexus Portal Chamber is crafted from polished black obsidian, which reflects the shimmering light from the crystals above, creating the impression that one is standing on a sea of stars. The chamber's perimeter is lined with towering, intricately carved stone pillars, each adorned with runic symbols and glowing energy patterns that pulse and swirl with cosmic power\r\n\r\nAt the center of the chamber stands the Nexus Portal itself: a large, circular gateway made of a mysterious, indestructible alloy. The frame of the \x1b[96mportal\x1b[0m is engraved with ancient glyphs and symbols, and it emanates a powerful, almost palpable energy. Within the frame, a swirling vortex of shimmering, translucent energy pulses and shifts, providing a mesmerizing window into the infinite realms that lie beyond. Arrayed around the perimeter of the portal are several control stations, each comprised of a raised \x1b[93mdais\x1b[0m with a variety of crystalline consoles and touch-sensitive panels. These stations allow users to select their desired destination, calibrate the portal's energy signature, and initiate the travel process.",
            objectsInRoom: new Dictionary<string, string>()
            {
                { "portal", "The Nexus Portal, standing at the heart of the chamber, is an imposing and majestic structure. The portal's frame is made of the unique and indestructible kraitoz alloy, a rare and valuable material forged from a combination of copper and limbo stones. This alloy is known for its incredible strength, durability, and ability to conduct and store vast amounts of energy, making it an ideal material for constructing the portal.\r\n\r\nKraitoz alloy has a rich, metallic hue with subtle iridescent streaks running through it, reminiscent of the copper from which it is partially derived. The limbo stones within the alloy add a mysterious depth to the material, with faint, swirling patterns that seem to move and change when viewed from different angles. This distinctive appearance gives the portal an air of otherworldliness, as if it were a gateway to realms beyond comprehension.\r\n\r\nThe frame of the Nexus Portal is adorned with intricate engravings and ancient glyphs, etched into the kraitoz alloy with precision and care. These symbols seem to glow with an inner light, pulsing in time with the energy that flows through the portal. The frame is fashioned into a series of concentric circles, each containing its own set of runes, which are believed to represent various cosmic destinations and the corresponding energy signatures required to access them.\r\n\r\nThe heart of the portal is a mesmerizing vortex of swirling, translucent energy. This vortex appears as a kaleidoscope of colors, constantly shifting and changing as it connects to different realms and dimensions. It exudes a sense of immense power, as if it were a living, breathing conduit to the vast reaches of the cosmos." },
                { "dais", "A series of shallow, gently curving steps lead up to the dais, allowing for easy access to the platform. The steps are flanked by two imposing statues, sculpted from the same kraitoz alloy. These statues represent guardian figures, their features a harmonious blend of various cosmic entities, symbolizing the unity and cooperation required to navigate the vast reaches of the cosmos.\r\n\r\nThe surface of the dais is engraved with an intricate network of lines and runes that mirror the glyphs found on the Nexus Portal frame. These engravings are more than just decorative; they serve as a control mechanism for the portal, allowing those who stand on the dais to input the desired destination and activate the portal. The runes glow softly, awaiting the touch of a knowledgeable hand to guide the portal's connection to distant realms." },

            },
            north: -1,
            south: 54,
            east: -1,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
                                                                new Room(
            name: "Outside the Silent Cemetery Portal",
            id: 58,
            description: "You find yourself standing outside of a particular portal that leads to the Silent Cemetery. The portal's swirling vortex appears dark and foreboding, with shades of deep indigo and midnight blue. The air around it seems to grow colder as you approach, and a palpable sense of melancholy and loss emanates from within.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: -1,
            east: -1,
            west: 7,
            canRecall: true,
            forageableItems: noForagableItems,
            permanentPortal: 59
        ),
        new Room(
            name: "Outside the Nexus Portal",
            id: 59,
            description: "You find yourself on a rocky ledge overlooking a steep cliff, the jagged edges of the rock formation beneath your feet threatening to crumble at any moment. The air is heavy with the scent of decay, and a chilling mist clings to the ground, obscuring your view of the landscape below. Behind you, the portal leading back to the Nexus swirls enticingly, a beacon of light and color in this otherwise dark and desolate place. The frame of the portal casts a faint, eerie glow onto the surrounding rocks, casting twisted shadows that seem to dance and merge with the wisps of fog. The cemetery itself sprawls out before you, a sea of weathered tombstones and crypts that seem to stretch into eternity. A palpable sense of loss and despair hangs over the area, as if the weight of countless souls is bearing down upon you. The silence is almost deafening, broken only by the distant howl of the wind and the faint, ghostly whispers carried on the breeze.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: -1,
            east: 60,
            west: -1,
            canRecall: false,
            forageableItems: noForagableItems,
            permanentPortal: 7
        ),
        new Room(
            name: "Rocky Tunnel",
            id: 60,
            description: "The entrance of the tunnel is partially obscured by twisted, gnarled roots that snake their way around the jagged rocks, as if attempting to claim the passage for themselves. The mouth of the tunnel is shrouded in shadow, a yawning void that seems to beckon you deeper into the unknown. As you make your way through the tunnel, you notice the walls are a mix of rough, unyielding stone and compacted earth. The ground underfoot is uneven, and you find yourself carefully navigating the treacherous terrain to avoid stumbling. Dim, flickering light filters in from small, irregular cracks in the tunnel's ceiling, casting eerie shadows that play tricks on your eyes, making the passageway feel more constricting than it truly is. The air within the tunnel is damp and musty, the lingering scent of decay growing stronger as you venture further in. The sound of your footsteps echoes off the walls, creating an unsettling rhythm that seems to reverberate through the very core of the earth.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: -1,
            east: 61,
            west: 59,
            canRecall: false,
            forageableItems: noForagableItems,
            indoor: true

        ),
        new Room(
            name: "Silent Cemetery Gates",
            id: 61,
            description: "Standing outside the cemetery gates, the imposing wrought-iron barrier casts a foreboding shadow, with intricate, twisted patterns that seem to dance in the dim light. The gate's towering arch looms above you, adorned with ornate symbols and iconography that suggest a rich and storied history. You can sense an unsettling energy emanating from beyond the gates, as if the air itself is heavy with the weight of countless souls. As you stand at the threshold of the Silent Cemetery, you can't help but feel a sense of unease. The stillness is palpable, interrupted only by the occasional rustle of wind through the dry, brittle branches of the gnarled trees that surround the area. Their twisted forms create a macabre silhouette against the darkening sky, casting eerie shadows that dance across the ground like restless spirits.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: 61,
            east: -1,
            west: 60,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
        new Room(
            name: "Silent Cemetery",
            id: 62,
            description: "The landscape is a patchwork of graves, crypts, and mausoleums, each one a testament to the countless souls that have been laid to rest here. The grass beneath your feet is a sickly, muted green, with patches of dry, cracked earth revealing the passage of time and the land's slow surrender to decay. Towering, ancient trees loom over the cemetery, their gnarled branches casting twisted shadows across the graves. These trees, some dead and others barely clinging to life, appear to be mourning the souls buried beneath them, their branches swaying and creaking like whispered laments on the wind.\r\n\r\nThe tombstones themselves are a mix of ornate and simple, with weathered inscriptions that hint at the stories of the lives that have passed. Some stones are adorned with statues of weeping angels, while others are marked by simple crosses or nothing more than a name and date. In the farthest corners of the cemetery, you can see the crumbling facades of crypts and mausoleums, their once-grand entrances now choked with creeping vines and moss.\r\n\r\nA persistent fog clings to the ground, weaving its way through the tombstones and monuments, as if the very essence of the cemetery is attempting to obscure its secrets from prying eyes. As you walk through the Silent Cemetery, you cannot shake the feeling that you are being watched by unseen eyes, the weight of the past bearing down on you with every step.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: 61,
            south: 63,
            east: -1,
            west: 66,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
        new Room(
            name: "Silent Cemetery",
            id: 63,
            description: "The terrain is a mosaic of burial plots, vaults, and sepulchers, each bearing witness to the innumerable souls interred within. The grass underfoot is a dull, unhealthy green, interspersed with areas of parched, fissured soil that expose the relentless march of time and the gradual decay of the land. Majestic, venerable trees tower above the cemetery, their twisted limbs casting eerie shadows upon the graves. These trees, some lifeless and others barely surviving, seem to grieve for the souls resting below, their boughs swaying and groaning like quiet dirges carried on the breeze.\r\n\r\nThe gravestones display a variety of ornate and modest designs, their weather-beaten inscriptions offering glimpses into the stories of those who once lived. Some markers are graced with sculptures of sorrowful angels, while others bear straightforward crosses or only the barest details of a name and date. In the most remote sections of the cemetery, the deteriorating exteriors of crypts and mausoleums are visible, their once-imposing entrances now overgrown with creeping vegetation and moss.\r\n\r\nA tenacious mist clings to the earth, winding its way amid the headstones and monuments as though the very spirit of the cemetery seeks to shroud its mysteries from inquisitive gazes. As you stroll through the Silent Cemetery, a sensation of being observed by invisible eyes lingers, the burden of history pressing down upon you with each step you take.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: 62,
            south: -1,
            east: -1,
            west: 64,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
        new Room(
            name: "Silent Cemetery",
            id: 64,
            description: "The grounds consist of a collection of gravesites, tombs, and resting places, each serving as a silent tribute to the countless souls laid to rest here. The grass beneath your feet is a lifeless, sickly green, interspersed with patches of dry, cracked earth that reveal the relentless passage of time and the slow encroachment of decay.\r\n\r\nImposing, ancient trees stand tall over the cemetery, their contorted branches casting unsettling shadows on the graves below. These trees, some devoid of life and others just barely hanging on, seem to mourn the souls interred beneath them, their limbs swaying and creaking like hushed elegies on the wind.\r\n\r\nThe tombstones come in a range of elaborate and humble designs, with their weather-worn inscriptions providing a window into the lives of those who once walked this world. Some gravestones are embellished with sculptures of grieving angels, while others display simple crosses or just the most basic information of a name and date. In the farthest reaches of the cemetery, the decaying facades of crypts and mausoleums can be seen, their once-grand entryways now engulfed by creeping vines and moss.\r\n\r\nA stubborn fog hugs the ground, meandering between the headstones and monuments as if the very essence of the cemetery aims to conceal its enigmas from curious onlookers. As you wander through the Silent Cemetery, a feeling of being watched by unseen observers persists, the weight of the past bearing down upon you with each footfall.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: 66,
            south: -1,
            east: 63,
            west: 65,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
        new Room(
            name: "Silent Cemetery",
            id: 65,
            description: "The area is an assortment of graves, tombs, and final resting places, each representing a solemn homage to the many souls entombed here. The grass at your feet is a faded, unhealthy green, dotted with areas of desiccated, fractured soil that expose the unyielding progression of time and the gradual advance of decomposition.\r\n\r\nMagnificent, age-old trees loom over the cemetery, their distorted branches casting disquieting shadows upon the graves beneath. These trees, some lifeless and others just barely surviving, appear to lament the souls interred below, their branches swaying and groaning like subdued requiems carried by the breeze.\r\n\r\nThe headstones exhibit an array of intricate and plain designs, their weathered inscriptions offering insight into the narratives of those who have passed. Some markers are adorned with statues of grieving angels, while others display austere crosses or solely the essential details of a name and date. In the most remote corners of the cemetery, the eroding exteriors of crypts and mausoleums are discernible, their once-magnificent entrances now overwhelmed by tendrils of ivy and layers of moss.\r\n\r\nA tenacious mist clings low to the ground, threading its way among the tombstones and monuments as though the very soul of the cemetery seeks to veil its secrets from inquiring eyes. As you meander through the Silent Cemetery, an unshakable feeling of being scrutinized by invisible watchers lingers, the pressure of history bearing down on you with every stride you take.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: -1,
            east: 64,
            west: -1,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
        new Room(
            name: "Silent Cemetery",
            id: 66,
            description: "The expanse is a compilation of graves, crypts, and eternal resting spots, each symbolizing a reverent tribute to the countless souls buried here. The grass underfoot is a washed-out, ailing green, speckled with patches of dehydrated, cracked earth that expose the relentless advance of time and the slow onset of decay.\r\n\r\nGrand, ancient trees overshadow the cemetery, their warped branches casting eerie shadows on the graves below. These trees, some dead and others barely holding on to life, seem to mourn the souls buried beneath them, their limbs swaying and creaking like hushed laments carried on the wind.\r\n\r\nThe gravestones showcase a variety of elaborate and unadorned designs, their weather-beaten inscriptions providing glimpses into the lives of those who are no more. Some stones are decorated with statues of tearful angels, while others present stark crosses or only the most basic information of a name and date. In the farthest reaches of the cemetery, the crumbling facades of crypts and mausoleums are noticeable, their once-stately entrances now smothered by creeping vines and blankets of moss.\r\n\r\nA persistent fog hugs the earth, weaving its way through the tombstones and monuments as if the very core of the cemetery aims to cloak its mysteries from prying eyes. As you stroll through the Silent Cemetery, a sensation of being observed by unseen presences persists, the burden of the past pressing down on you with each step you take.",
            objectsInRoom: new Dictionary<string, string>()
            {
            },
            north: -1,
            south: 64,
            east: -62,
            west: -1,
            canRecall: false,
            forageableItems: cemeteryForagableItems
        ),
                new Room(
                    name: "Mechatropolis Docks",
                    id: 67,
                    description: "Placeholder text for Mechatropolis Docks",
                               objectsInRoom: new Dictionary<string, string>()
                               {
                               },
                               north: -1,
                    south: -1,
                    east: -1,
                    west: -1,
                    canRecall: false,
                    forageableItems: noForagableItems,
                    hasVendingMachine: true

                )
    };

    // Exits
    _rooms[0].AddExit("north", _rooms[1]); // penthouse to powder room
    _rooms[1].AddExit("north", _rooms[2]); // powder room to closer
    _rooms[1].AddExit("south", _rooms[0]); // powder room to penthouse
    _rooms[1].AddExit("west", _rooms[5]); // powder room to shower
    _rooms[2].AddExit("south", _rooms[1]); // closet to powder room
    _rooms[0].AddExit("south", _rooms[4]); // penthouse to hallway
    _rooms[4].AddExit("north", _rooms[0]); // hallway to penthouse
    _rooms[4].AddExit("south", _rooms[3]); // hallway to elevator
    _rooms[3].AddExit("north", _rooms[4]); // elevator to hallway
    _rooms[5].AddExit("east", _rooms[1]); // shower to powder room
    _rooms[6].AddExit("north", _rooms[11]); // nexus to xuolia tunnel
    _rooms[11].AddExit("south", _rooms[6]); // xuolia tunnel to nexus
    _rooms[6].AddExit("south", _rooms[7]); // nexus to saquia portal
    _rooms[7].AddExit("north", _rooms[6]); // saquia portal to nexus
    _rooms[8].AddExit("south", _rooms[9]); // saquia docks to seaside path
    _rooms[9].AddExit("north", _rooms[8]); // seaside path to saquia docks
    _rooms[9].AddExit("south", _rooms[12]); // seaside path to saquia trade district
    _rooms[12].AddExit("north", _rooms[9]); // saquia trade district to seaside path
    _rooms[12].AddExit("east", _rooms[13]); // saquia trade district to beachside nook
    _rooms[13].AddExit("west", _rooms[12]); // beachside nook to saquia trade district
    _rooms[12].AddExit("west", _rooms[14]); // saquia trade district to saquia pavilion
    _rooms[14].AddExit("east", _rooms[12]); // saquia pavilion to saquia trade district
    _rooms[14].AddExit("south", _rooms[16]); // saquia pavilion to saquia beachside gardens
    _rooms[16].AddExit("north", _rooms[14]); // beachside gardens to pavilion
    _rooms[12].AddExit("south", _rooms[15]); // saquia trade district to gardens path
    _rooms[15].AddExit("north", _rooms[12]); // gardens path to saquia trade district
    _rooms[15].AddExit("west", _rooms[16]); // gardens path to saquia gardens
    _rooms[16].AddExit("east", _rooms[15]); // saquia gardens to gardens path
    _rooms[15].AddExit("south", _rooms[17]); // gardens path to saquia gardens
    _rooms[17].AddExit("north", _rooms[15]); // saquia gardens to gardens path
    _rooms[17].AddExit("west", _rooms[18]); // hillside path to water fountain
    _rooms[18].AddExit("east", _rooms[17]); // water fountain to hillside path
    _rooms[18].AddExit("south", _rooms[19]); // water fountain to marble palace exterior
    _rooms[19].AddExit("north", _rooms[18]); // marble palace exterior to water fountain
    _rooms[17].AddExit("east", _rooms[20]); // hillside path to marble pathway
    _rooms[20].AddExit("west", _rooms[17]); // marble pathway to hillside path
    _rooms[20].AddExit("north", _rooms[21]); // marble pathway to lush path
    _rooms[21].AddExit("south", _rooms[20]); // lush path to marble pathway
    _rooms[21].AddExit("east", _rooms[22]); // lush path to saquia hanging gardens
    _rooms[22].AddExit("west", _rooms[21]); // saquia hanging gardens to lush path
    _rooms[20].AddExit("east", _rooms[23]); // marble pathway to saquia residential district
    _rooms[23].AddExit("west", _rooms[20]); // saquia residential district to marble pathway
    _rooms[23].AddExit("east", _rooms[24]); // saquia residential district to saquia chapel
    _rooms[24].AddExit("west", _rooms[23]); // saquia chapel to saquia residential district
    _rooms[25].AddExit("north", _rooms[20]); // blue cherry grove to marble pathway
    _rooms[20].AddExit("south", _rooms[25]); // marble pathway to blue cherry grove
    _rooms[11].AddExit("north", _rooms[27]); // xuolia tunnel to xuolia foyer
    _rooms[27].AddExit("south", _rooms[11]); // xuolia foyer to xuolia tunnel
    _rooms[27].AddExit("east", _rooms[28]); // xuolia foyer to xuolia visitor's center
    _rooms[28].AddExit("west", _rooms[27]); // xuolia visitor's center to xuolia foyer
    _rooms[27].AddExit("west", _rooms[29]); // xuolia foyer to xuolia chapel
    _rooms[29].AddExit("east", _rooms[27]); // xuolia chapel to xuolia foyer
    _rooms[27].AddExit("north", _rooms[30]); // xuolia foyer to xuolia mana fountain
    _rooms[30].AddExit("south", _rooms[27]); // xuolia mana fountain to xuolia foyer
    _rooms[30].AddExit("west", _rooms[10]); // xuolia foyer to xuolia library
    _rooms[10].AddExit("east", _rooms[30]); // xuolia library to xuolia foyer
    _rooms[30].AddExit("north", _rooms[31]); // xuolia foyer to xuolia marketplace
    _rooms[31].AddExit("south", _rooms[30]); // xuolia marketplace to xuolia foyer
    _rooms[31].AddExit("west", _rooms[32]); // xuolia marketplace to xuolia dragon statue
    _rooms[32].AddExit("east", _rooms[31]); // xuolia dragon statue to xuolia marketplace
    _rooms[32].AddExit("west", _rooms[33]); // xuolia dragon statue to afterdark lounge
    _rooms[33].AddExit("east", _rooms[32]); // afterdark lounge to xuolia dragon statue
    _rooms[31].AddExit("north", _rooms[34]); // xuolia marketplace to xuolia waterfront
    _rooms[34].AddExit("south", _rooms[31]); // xuolia waterfront to xuolia marketplace
    _rooms[34].AddExit("east", _rooms[35]); // xuolia waterfront to strange door
    _rooms[35].AddExit("west", _rooms[34]); // strange door to xuolia waterfront
    _rooms[34].AddExit("west", _rooms[36]); // xuolia waterfront to club crimson exterior
    _rooms[36].AddExit("east", _rooms[34]); // club crimson exterior to xuolia waterfront
    _rooms[6].AddExit("west", _rooms[37]); // nexus to outside the drybone portal
    _rooms[37].AddExit("east", _rooms[6]); // outside the drybone portal to nexus
    _rooms[38].AddExit("east", _rooms[39]); // drybone docks to drybone lighthouse
    _rooms[39].AddExit("north", _rooms[40]); // drybone lighthouse to drybone airship dock
    _rooms[40].AddExit("south", _rooms[39]); // drybone airship dock to drybone lighthouse
    _rooms[38].AddExit("north", _rooms[41]); // drybone docks to drybone cove
    _rooms[41].AddExit("south", _rooms[38]); // drybone cove to drybone docks
    _rooms[41].AddExit("north", _rooms[42]); // drybone cove to drybone cove
    _rooms[42].AddExit("south", _rooms[41]); // drybone cove to drybone cove
    _rooms[42].AddExit("north", _rooms[43]); // drybone cove to drybone cove
    _rooms[43].AddExit("south", _rooms[42]); // drybone cove to drybone cove
    _rooms[43].AddExit("north", _rooms[44]); // drybone cove to drybone beach
    _rooms[44].AddExit("east", _rooms[45]); // drybone beach to drybone beach
    _rooms[45].AddExit("west", _rooms[44]); // drybone beach to drybone beach
    _rooms[45].AddExit("east", _rooms[46]); // drybone beach to drybone beach
    _rooms[46].AddExit("west", _rooms[45]); // drybone beach to drybone beach
    _rooms[46].AddExit("east", _rooms[47]); // drybone beach to drybone beach
    _rooms[47].AddExit("west", _rooms[46]); // drybone beach to drybone beach
    _rooms[46].AddExit("north", _rooms[48]); // drybone beach to drybone beach
    _rooms[48].AddExit("south", _rooms[46]); // drybone beach to drybone beach
    _rooms[45].AddExit("north", _rooms[49]); // drybone beach to drybone beach
    _rooms[49].AddExit("south", _rooms[45]); // drybone beach to drybone beach
    _rooms[48].AddExit("west", _rooms[49]); // drybone beach to drybone beach
    _rooms[49].AddExit("east", _rooms[48]); // drybone beach to drybone beach
    _rooms[39].AddExit("west", _rooms[38]); // drybone lighthouse to drybone docks
    _rooms[44].AddExit("south", _rooms[43]); // drybone beach to drybone cove
    _rooms[50].AddExit("west", _rooms[51]); // afterdark lounge basement to seedy room
    _rooms[51].AddExit("east", _rooms[50]); // seedy room to afterdark lounge basement
    _rooms[50].AddExit("east", _rooms[52]); // afterdark basement to vip room
    _rooms[52].AddExit("west", _rooms[50]); // vip room to afterdark basement
    _rooms[26].AddExit("north", _rooms[53]); // plane of dream to dreamweavers hall
    _rooms[53].AddExit("south", _rooms[26]); // dreamweavers hall to plane of dream
    _rooms[53].AddExit("west", _rooms[54]); // dreamweavers hall to chamber of memories
    _rooms[54].AddExit("east", _rooms[53]); // chamber of memories to dreamweavers hall
    _rooms[53].AddExit("east", _rooms[55]); // dreamweavers hall to archive of tomes
    _rooms[55].AddExit("west", _rooms[53]); // archive of tomes to dreamweavers hall
    _rooms[53].AddExit("north", _rooms[56]); // dreamweavers hall to nexus portal chamber
    _rooms[56].AddExit("south", _rooms[53]); // nexus portal chamber to dreamweavers hall
    _rooms[6].AddExit("east", _rooms[57]); // nexus to outside the silent cemetery portal
    _rooms[57].AddExit("west", _rooms[6]); // outside the silent cemetery portal to nexus
    _rooms[58].AddExit("east", _rooms[59]); // outside the nexus portal to rocky tunnel
    _rooms[59].AddExit("west", _rooms[58]); // rocky tunnel to outside the nexus portal
    _rooms[59].AddExit("east", _rooms[60]); // rocky tunnel to silent cemetery gates
    _rooms[60].AddExit("west", _rooms[59]); // silent cemetery gates to rocky tunnel
    _rooms[60].AddExit("south", _rooms[61]); // silent cemetery gates to silent cemetery
    _rooms[61].AddExit("north", _rooms[60]); // silent cemetery to silent cemetery gates
    _rooms[61].AddExit("south", _rooms[62]); // silent cemetery to silent cemetery
    _rooms[62].AddExit("north", _rooms[61]); // silent cemetery to silent cemetery
    _rooms[62].AddExit("west", _rooms[63]); // silent cemetery to silent cemetery
    _rooms[63].AddExit("east", _rooms[62]); // silent cemetery to silent cemetery
    _rooms[63].AddExit("west", _rooms[64]); // silent cemetery to silent cemetery
    _rooms[64].AddExit("east", _rooms[63]); // silent cemetery to silent cemetery
    _rooms[65].AddExit("south", _rooms[63]); // silent cemetery to silent cemetery
    _rooms[63].AddExit("north", _rooms[65]); // silent cemetery to silent cemetery
    _rooms[61].AddExit("west", _rooms[65]); // silent cemetery to silent cemetery
    _rooms[65].AddExit("east", _rooms[61]); // silent cemetery to silent cemetery
    


}
private void InitializeNPCs()
{
    // Create a list to store all the NPCs
    _npcs = new List<NPC>();

    // Get the Sweet Smelling Orb item from the database
    Item sweetSmellingOrb = ItemManager.GetItem(12);

    // Get the Dream Castle Key item from the database
    Item dreamCastleKey = ItemManager.GetItem(13);

    // Create a loot list containing the Sweet Smelling Orb item
    var lootList = new Dictionary<int, Item>
    {
        { sweetSmellingOrb.ID, sweetSmellingOrb }
    };

    // Create a loot list containing the Dream Castle Key item
    var dreamGuardianDiZolpemlootList = new Dictionary<int, Item>
    {
        { dreamCastleKey.ID, dreamCastleKey }
    };

// Create the kitty NPC
    List<int> allowedRooms = new List<int> { 1, 2, 3 };
    NPC movingNPC = new NPC(this, 1, "Kitty", 5, 1, 100, 100, 100, 100, "A beautifully fat black cat.", allowedRooms, 1, 1, lootList, "", false, 10);

// Clone and add the NPC to the _npcs list
    _npcs.Add(movingNPC.Clone());


    // Create the sorcery bound sentinel 2
    List<int> sentinel1AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC sorceryBoundSentinel = new NPC(this,2, "Sorcery Bound Sentinel 2", 5, 10, 50, 50, 50, 50, "A towering sentinel, bound in sorcery.", sentinel1AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sorceryBoundSentinel.Clone());
    
    // Create the sorcery bound sentinel 4
    List<int> sentinel4AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC sorceryBoundSentinel4 = new NPC(this,4, "Sorcery Bound Sentinel 8", 5, 13, 50, 50, 50, 50, "A towering sentinel, bound in sorcery.", sentinel4AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sorceryBoundSentinel4.Clone());
    // Create the sorcery bound sentinel 5
    List<int> sentinel5AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC sorceryBoundSentinel5 = new NPC(this,5, "Sorcery Bound Sentinel 5", 5, 24, 50, 50, 50, 50, "A towering sentinel, bound in sorcery.", sentinel5AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sorceryBoundSentinel5.Clone());
    // Create the sorcery bound sentinel 6
    List<int> sentinel6AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC sorceryBoundSentinel6 = new NPC(this,6, "Sorcery Bound Sentinel 6", 5, 19, 50, 50, 50, 50, "A towering sentinel, bound in sorcery.", sentinel6AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sorceryBoundSentinel6.Clone());
    // Create the sorcery bound sentinel 7
    List<int> sentinel7AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC sorceryBoundSentinel7 = new NPC(this,7, "Sorcery Bound Sentinel 7", 5, 16, 50, 50, 50, 50, "A towering sentinel, bound in sorcery.", sentinel7AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sorceryBoundSentinel7.Clone());
    // Create the saquia thrall NPC 1
    List<int> saquiaThrall1AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaThrall1 = new NPC(this,8, "S`Aquia Thrall 1", 5, 16, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaThrall1AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaThrall1.Clone());
    // Create the saquia thrall NPC 2
    List<int> saquiaThrall2AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaThrall2 = new NPC(this,9, "S`Aquia Thrall 2", 5, 19, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaThrall2AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaThrall2.Clone());
    // Create the saquia thrall NPC 3
    List<int> saquiaThrall3AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaThrall3 = new NPC(this,10, "S`Aquia Thrall 3", 5, 24, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaThrall3AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaThrall3.Clone());
    // Create the saquia thrall NPC 4
    List<int> saquiaThrall4AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaThrall4 = new NPC(this,11, "S`Aquia Thrall 4", 5, 13, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaThrall4AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaThrall4.Clone());
    // Create the saquia thrall NPC 5
    List<int> saquiaThrall5AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaThrall5 = new NPC(this,12, "S`Aquia Thrall 5", 5, 10, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaThrall5AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaThrall5.Clone());
    // Create the saquia siren NPC 1
    List<int> saquiaSiren1AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaSiren1 = new NPC(this,8, "S`Aquia Siren 1", 5, 16, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaSiren1AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaSiren1.Clone());
    // Create the saquia siren NPC 2
    List<int> saquiaSiren2AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaSiren2 = new NPC(this,9, "S`Aquia Siren 2", 5, 19, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaSiren2AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaSiren2.Clone());
    // Create the saquia siren NPC 3
    List<int> saquiaSiren3AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaSiren3 = new NPC(this,10, "S`Aquia Siren 3", 5, 24, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaSiren3AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaSiren3.Clone());
    // Create the saquia Siren NPC 4
    List<int> saquiaSiren4AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaSiren4 = new NPC(this,11, "S`Aquia Siren 4", 5, 13, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaSiren4AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaSiren4.Clone());
    // Create the saquia Siren NPC 5
    List<int> saquiaSiren5AllowedRooms = new List<int>{10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26};
    NPC saquiaSiren5 = new NPC(this,12, "S`Aquia Siren 5", 5, 10, 50, 50, 50, 50, "You almost feel sorry for them, but they seem happy.", saquiaSiren5AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(saquiaSiren5.Clone());
    // Create the bathing Siren NPC 1
    List<int> bathingSiren1AllowedRooms = new List<int>{14};
    NPC bathingSiren1 = new NPC(this,13, "Bathing Siren 1", 3, 14, 30, 30, 30, 30, "She hums a lovely tune as she washes her hair with a kelp-based soap.", bathingSiren1AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(bathingSiren1.Clone());
    // Create the bathing Siren NPC 2
    List<int> bathingSiren2AllowedRooms = new List<int>{14};
    NPC bathingSiren2 = new NPC(this,14, "Bathing Siren 2", 3, 14, 30, 30, 30, 30, "She hums a lovely tune as she washes her hair with a kelp-based soap.", bathingSiren2AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(bathingSiren2.Clone());
    // Create the bathing Siren NPC 3
    List<int> bathingSiren3AllowedRooms = new List<int>{14};
    NPC bathingSiren3 = new NPC(this,15, "Bathing Siren 3", 3, 14, 30, 30, 30, 30, "She hums a lovely tune as she washes her hair with a kelp-based soap.", bathingSiren3AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(bathingSiren3.Clone());
    // Create the sand crab NPC 1
    List<int> sandCrab1AllowedRooms = new List<int>{14};
    NPC sandCrab1 = new NPC(this,16, "Sand Crab 1", 1, 14, 10, 10, 10, 10, "A cute little crab, innocently skittering across the rocks.", sandCrab1AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sandCrab1.Clone());
    Room startingRoom21 = GetRoomById(sandCrab1.StartingRoomId);
    startingRoom21.NPCsInRoom.Add(sandCrab1);
    // Start the NPC movement task
    Task.Run(() => sandCrab1.MoveBetweenRooms(this));
    // Create the sand crab NPC 2
    List<int> sandCrab2AllowedRooms = new List<int>{14};
    NPC sandCrab2 = new NPC(this,17, "Sand Crab 2", 1, 14, 10, 10, 10, 10, "A cute little crab, innocently skittering across the rocks.", sandCrab2AllowedRooms, 1, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(sandCrab2.Clone());
    Room startingRoom22 = GetRoomById(sandCrab2.StartingRoomId);
    startingRoom22.NPCsInRoom.Add(sandCrab2);
    // Start the NPC movement task
    Task.Run(() => sandCrab2.MoveBetweenRooms(this));
    // Create the Dream Guardian Di`Zolpem
    List<int> dreamGuardianDiZolpemAllowedRooms = new List<int>{27};
    NPC dreamGuardianDiZolpem = new NPC(this,18, "Dream Guardian Di`Zolpem", 1, 27, 5, 5, 5, 5, "Despite its small size, the imp exudes a powerful sense of confidence and energy. You can't quite put your finger on it, but there's something about this little creature that makes you feel as if you're in the presence of something truly extraordinary.\r\nHINT: You can 'con <entity name>' to see more information about an entity, such as their difficulty level.\x1b[0m", dreamGuardianDiZolpemAllowedRooms, 10, 1, CloneLootList(dreamGuardianDiZolpemlootList), "Welcome, Dreamer. Don't you know where you are? Why... this is the Plane of Dream, of course. If you want to enter the Castle of Dreams, you'll need to defeat me... why, of course, I doubt you can... so you might as well just wake up. But, if you wish you try, just 'attack' me and we'll... see how it goes....\r\n\r\n[92mHINT: You can 'con <target name>' to learn more about an NPC, including its difficulty level.\x1b[0m\r\n", false, 10);
    _npcs.Add(dreamGuardianDiZolpem.Clone());
    Room startingRoom23 = GetRoomById(dreamGuardianDiZolpem.StartingRoomId);
    startingRoom23.NPCsInRoom.Add(dreamGuardianDiZolpem);
    
    // Create the silent skeleton npc 1
    List<int> silentSkeleton1AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC silentSkeleton1 = new NPC(this,32, "Silent Skeleton 1", 1, 62, 10, 10, 10, 10, "Brittle bones, still walking about.", silentSkeleton1AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(silentSkeleton1.Clone());
    Room startingRoom32 = GetRoomById(silentSkeleton1.StartingRoomId);
    startingRoom32.NPCsInRoom.Add(silentSkeleton1);
    // Start the NPC movement task
    Task.Run(() => silentSkeleton1.MoveBetweenRooms(this));
    
    // Create the silent skeleton npc 2
    List<int> silentSkeleton2AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC silentSkeleton2 = new NPC(this,33, "Silent Skeleton 2", 1, 63, 10, 10, 10, 10, "Brittle bones, still walking about.", silentSkeleton2AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(silentSkeleton2.Clone());
    Room startingRoom33 = GetRoomById(silentSkeleton2.StartingRoomId);
    startingRoom33.NPCsInRoom.Add(silentSkeleton2);
    // Start the NPC movement task
    Task.Run(() => silentSkeleton2.MoveBetweenRooms(this));

    // Create the silent skeleton npc 3
    List<int> silentSkeleton3AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC silentSkeleton3 = new NPC(this,34, "Silent Skeleton 3", 1, 64, 10, 10, 10, 10, "Brittle bones, still walking about.", silentSkeleton3AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(silentSkeleton3.Clone());
    Room startingRoom34 = GetRoomById(silentSkeleton3.StartingRoomId);
    startingRoom34.NPCsInRoom.Add(silentSkeleton3);
    // Start the NPC movement task
    Task.Run(() => silentSkeleton3.MoveBetweenRooms(this));
    
    // Create the silent skeleton npc 4
    List<int> silentSkeleton4AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC silentSkeleton4 = new NPC(this,35, "Silent Skeleton 4", 1, 65, 10, 10, 10, 10, "Brittle bones, still walking about.", silentSkeleton4AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(silentSkeleton4.Clone());
    Room startingRoom35 = GetRoomById(silentSkeleton4.StartingRoomId);
    startingRoom35.NPCsInRoom.Add(silentSkeleton4);
    // Start the NPC movement task
    Task.Run(() => silentSkeleton4.MoveBetweenRooms(this));

    // Create the silent skeleton npc 5
    List<int> silentSkeleton5AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC silentSkeleton5 = new NPC(this,36, "Silent Skeleton 5", 1, 66, 10, 10, 10, 10, "Brittle bones, still walking about.", silentSkeleton5AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(silentSkeleton5.Clone());
    Room startingRoom36 = GetRoomById(silentSkeleton5.StartingRoomId);
    startingRoom36.NPCsInRoom.Add(silentSkeleton5);
    // Start the NPC movement task
    Task.Run(() => silentSkeleton5.MoveBetweenRooms(this));
    
    // Create the Corpse Slitherer npc 1
    List<int> corpseSlitherer1AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC corpseSlitherer1 = new NPC(this, 37, "Corpse Slitherer 1", 1, 65, 10, 10, 10, 10, "A snake with a swollen serpent stomach from snacking on skin.", corpseSlitherer1AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(corpseSlitherer1.Clone());
    Room startingRoom37 = GetRoomById(corpseSlitherer1.StartingRoomId);
    startingRoom35.NPCsInRoom.Add(corpseSlitherer1);
    // Start the NPC movement task
    Task.Run(() => corpseSlitherer1.MoveBetweenRooms(this));
    
    // Create the Corpse Slitherer npc 2
    List<int> corpseSlitherer2AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC corpseSlitherer2 = new NPC(this, 38, "Corpse Slitherer 2", 1, 63, 10, 10, 10, 10, "A snake with a swollen serpent stomach from snacking on skin.", corpseSlitherer2AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(corpseSlitherer2.Clone());
    Room startingRoom38 = GetRoomById(corpseSlitherer2.StartingRoomId);
    startingRoom36.NPCsInRoom.Add(corpseSlitherer2);
    // Start the NPC movement task
    Task.Run(() => corpseSlitherer2.MoveBetweenRooms(this));
    
    // Create the Corpse Slitherer npc 3
    List<int> corpseSlitherer3AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC corpseSlitherer3 = new NPC(this, 39, "Corpse Slitherer 3", 1, 64, 10, 10, 10, 10, "A snake with a swollen serpent stomach from snacking on skin.", corpseSlitherer3AllowedRooms, 9, 1, CloneLootList(lootList), "", false, 10);
    _npcs.Add(corpseSlitherer3.Clone());
    Room startingRoom39 = GetRoomById(corpseSlitherer3.StartingRoomId);
    startingRoom37.NPCsInRoom.Add(corpseSlitherer3);
    // Start the NPC movement task
    Task.Run(() => corpseSlitherer3.MoveBetweenRooms(this));
    
    // Create the Risen Child npc 1
    List<int> risenChild1AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC risenChild1 = new NPC(this, 40, "Risen Child 1", 2, 62, 15, 10, 15, 10, "A reanimated child, driven by dark forces.", risenChild1AllowedRooms, 9, 2, CloneLootList(lootList), "", false, 15);
    _npcs.Add(risenChild1.Clone());
    Room startingRoom40 = GetRoomById(risenChild1.StartingRoomId);
    startingRoom40.NPCsInRoom.Add(risenChild1);
// Start the NPC movement task
    Task.Run(() => risenChild1.MoveBetweenRooms(this));

// Create the Risen Child npc 2
    List<int> risenChild2AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC risenChild2 = new NPC(this, 41, "Risen Child 2", 2, 63, 15, 10, 15, 10, "A reanimated child, driven by dark forces.", risenChild2AllowedRooms, 9, 2, CloneLootList(lootList), "", false, 15);
    _npcs.Add(risenChild2.Clone());
    Room startingRoom41 = GetRoomById(risenChild2.StartingRoomId);
    startingRoom41.NPCsInRoom.Add(risenChild2);
// Start the NPC movement task
    Task.Run(() => risenChild2.MoveBetweenRooms(this));

// Create the Risen Child npc 3
    List<int> risenChild3AllowedRooms = new List<int>{62, 63, 64, 65, 66};
    NPC risenChild3 = new NPC(this, 42, "Risen Child 3", 2, 64, 15, 10, 15, 10, "A reanimated child, driven by dark forces.", risenChild3AllowedRooms, 9, 2, CloneLootList(lootList), "", false, 15);
    _npcs.Add(risenChild3.Clone());
    Room startingRoom42 = GetRoomById(risenChild3.StartingRoomId);
    startingRoom42.NPCsInRoom.Add(risenChild3);
// Start the NPC movement task
    Task.Run(() => risenChild3.MoveBetweenRooms(this));
    
        // Create Hedgecrafter Louise Lemon NPC
    List<int> louiseLemonRooms = new List<int> { 43 };
    NPC louiseLemon = new NPC(this,43, "Hedgecrafter Louise Lemon", 50, 35, 10000, 10000, 10000, 10000,  "A cute young Hedgecrafter, kneeling before the water's edge, filling a clay pot with the cool clear water..", louiseLemonRooms, 11, 1, CloneLootList(lootList), "I will teach you the ways of hedgecrafting",true, 10);
    louiseLemon.AvailableSpells["Plant Form"] = (Description: "Temporarily disguises you as a plant", RequiredLevel: 2, RequiredSkill: "Hedgecrafting", RequiredSkillValue: 1, 10);
    louiseLemon.AvailableSpells["Photosynthesis"] = (Description: "Draws energy from the sunlight, increasing your health regeneration. May only be cast outdoors during the day time.", RequiredLevel: 5, RequiredSkill: "Hedgecrafting", RequiredSkillValue: 5, 50);
    _npcs.Add(louiseLemon);
    Room startingRoom43 = GetRoomById(louiseLemon.StartingRoomId);
    if (startingRoom43 != null)
    {
        startingRoom43.NPCsInRoom.Add(louiseLemon);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{louiseLemon.Name}' to room with ID {louiseLemon.StartingRoomId}");
    }
    
    
    // Create Rianne Maltor NPC
    List<int> rianneRooms = new List<int> { 11 };
    NPC rianneMaltor = new NPC(this,3, "Rianne Maltor", 50, 11, 10000, 10000, 10000, 10000,  "A wise and experienced trainer of divine magic.", rianneRooms, 11, 1, CloneLootList(lootList), "I will teach you the ways of magic",true, 10);
    rianneMaltor.AvailableSpells["Blessing"] = (Description: "Temporarily blesses your target, increasing their maximum health", RequiredLevel: 2, RequiredSkill: "Faith", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Mindbright"] = (Description: "Temporarily boosts your target's clarity of mind, increasing their maximum mana", RequiredLevel: 2, RequiredSkill: "Sagacity", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Salve"] = (Description: "Instantly heals your target for a small amount", RequiredLevel: 2, RequiredSkill: "Woundtending", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Hot Radiation"] = (Description: "Temporarily engulfs your target in hot radiation, damaging anything that attacks them", RequiredLevel: 2, RequiredSkill: "Arcanism", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Coldfire"] = (Description: "Blasts your target with coldfire, instantly damaging them", RequiredLevel: 2, RequiredSkill: "Arcanism", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Telepathy"] = (Description: "A passive ability that allows usage of the 'tell' command to telepathically message another Soul", RequiredLevel: 2, RequiredSkill: "Psionics", RequiredSkillValue: 1, 10);
    rianneMaltor.AvailableSpells["Mass Telepathy"] = (Description: "A passive ability that allows usage of the 'general' command to telepathically message all connected Souls", RequiredLevel: 5, RequiredSkill: "Psionics", RequiredSkillValue: 2, 50);
    rianneMaltor.AvailableSpells["Cosmic Essence"] = (Description: "Temporarily fills your target with a cosmic essence, increasing their mana regeneration", RequiredLevel: 3, RequiredSkill: "Sagacity", RequiredSkillValue: 3, 30);
    rianneMaltor.AvailableSpells["Minor Illusion"] = (Description: "Temporarily disguises you as a common object", RequiredLevel: 3, RequiredSkill: "Illusion", RequiredSkillValue: 1, 30);
    rianneMaltor.AvailableSpells["Minor Familiar"] = (Description: "Calls forth a cosmetic familiar from the wild", RequiredLevel: 3, RequiredSkill: "Blue Magic", RequiredSkillValue: 1, 30);
    rianneMaltor.AvailableSpells["Whispering Wind"] = (Description: "Temporarily cloaks you in an illusory wind, obscuring your 'tells' from showing the sender", RequiredLevel: 5, RequiredSkill: "Illusion", RequiredSkillValue: 5, 30);


    _npcs.Add(rianneMaltor);
    Room startingRoom3 = GetRoomById(rianneMaltor.StartingRoomId);
    if (startingRoom3 != null)
    {
        startingRoom3.NPCsInRoom.Add(rianneMaltor);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{rianneMaltor.Name}' to room with ID {rianneMaltor.StartingRoomId}");
    }
    
    // Create Dizian Maltor NPC
    List<int> dizian1Rooms = new List<int> { 57 };
    NPC dizianMaltor1 = new NPC(this,30, "Dizian Maltor", 50, 57, 10000, 10000, 10000, 10000,  "Dizian Maltor is a striking figure who commands attention as soon as he enters a room. Standing at six feet tall, he has an athletic build and an air of confidence that radiates from him. His skin is a deep, coppery bronze, hinting at his frequent travels through various realms and his exposure to their myriad environments. Around his neck, Dizian wears a pendant crafted from a polished limbo stone set in a delicate copper frame, the same materials used in the construction of the kraitoz alloy. This pendant serves as a symbol of his connection to the Nexus Portal and his mastery over its secrets. As Dizian Maltor stands before the portal, he appears as a figure destined to explore the furthest reaches of the universe, driven by an unquenchable thirst for knowledge and adventure.", dizian1Rooms, 11, 1, CloneLootList(lootList), "It's a pleasure to meet someone with a similar passion for exploration and discovery. You may have heard of the recent changes in the cosmic pathways leading to Magnos. It seems the Frozen Purgatory has been sealed off, and travelers are now finding themselves in the Plane of Dream upon their entry to Magnos. This has caused quite a stir within our ranks, and we're working tirelessly to understand the implications of this shift. Our society is dedicated to the betterment of Magnos through the discovery and exploration of new dimensions. We believe that knowledge is the key to a prosperous future for all who dwell within our realm, and it is our sacred duty to uncover the mysteries of the cosmos.",true, 10, 10, 3, "''I have been studying your actions and movements since you arrived in this plane. Your curious mind and adventurous spirit are exactly what we need to help us understand the changes that have occurred in the cosmos. I have a task for you that will require us to travel to the Nexus and investigate the recent changes that have occurred there. Even in this limited capacity Magnos currently operates on, I believe together we may be able to get to the bottom of the corruption happening in our world. Do you wish to accept this task and join me as a Traveler?''\r\n\r\n\x1b[92mHINT: Type 'say I accept' if you wish to continue. You won't be able to explore the Plane of Dream after this point.\x1b[0m\r\n");
    dizianMaltor1.AvailableSpells["Spirit Recall"] = (Description: "Sends you to your soulbind location", RequiredLevel: 1, RequiredSkill: "Arcanism", RequiredSkillValue: 0, 0);
    dizianMaltor1.AvailableSpells["Blinkstep"] = (Description: "Instantly teleports you to a random connected room", RequiredLevel: 2, RequiredSkill: "Etherealism", RequiredSkillValue: 1, 10);
    _npcs.Add(dizianMaltor1);
    Room startingRoom30 = GetRoomById(dizianMaltor1.StartingRoomId);
    if (startingRoom30 != null)
    {
        startingRoom30.NPCsInRoom.Add(dizianMaltor1);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{dizianMaltor1.Name}' to room with ID {dizianMaltor1.StartingRoomId}");
    }
    
    
        // Create Dizian Maltor 2 NPC
    List<int> dizian2Rooms = new List<int> { 7 };
    NPC dizianMaltor2 = new NPC(this,31, "Dizian Maltor", 50, 7, 10000, 10000, 10000, 10000,  "Dizian Maltor is a striking figure who commands attention as soon as he enters a room. Standing at six feet tall, he has an athletic build and an air of confidence that radiates from him. His skin is a deep, coppery bronze, hinting at his frequent travels through various realms and his exposure to their myriad environments. Around his neck, Dizian wears a pendant crafted from a polished limbo stone set in a delicate copper frame, the same materials used in the construction of the kraitoz alloy. This pendant serves as a symbol of his connection to the Nexus Portal and his mastery over its secrets.", dizian2Rooms, 11, 1, CloneLootList(lootList), "Welcome to Magnos. Or rather, a corrupted version of it. Our research suggests that there's some sort of memory leak affecting the very fabric of this realm, twisting and distorting its true nature. It's crucial that we uncover the cause of this corruption and find a way to mend the rift before it's too late. But first, you'll need to familiarize yourself with this altered world and learn to navigate its challenges. Take your time to explore and acquaint yourself with this place. Learn its secrets, and gain a deeper understanding of the forces at play here. But always remember to tread carefully; the corruption has made this realm unpredictable and fraught with danger.",true, 10, 10, 3, "Dizian Maltor says, 'Once you feel ready, return to me, and we'll continue our mission together. The Society of Travelers has faith in your abilities, and I'm confident that you'll be a vital asset in our quest to save Magnos from this insidious corruption.'\r\n");
    dizianMaltor1.AvailableSpells["Spirit Recall"] = (Description: "Sends you to your soulbind location", RequiredLevel: 1, RequiredSkill: "Arcanism", RequiredSkillValue: 0, 0);
    _npcs.Add(dizianMaltor2);
    Room startingRoom31 = GetRoomById(dizianMaltor2.StartingRoomId);
    if (startingRoom31 != null)
    {
        startingRoom31.NPCsInRoom.Add(dizianMaltor2);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{dizianMaltor2.Name}' to room with ID {dizianMaltor2.StartingRoomId}");
    }
    
    
    
    
    
    

    // Create Ritualist Tenshi
    List<int> tenshiRooms = new List<int> { 52 };
    NPC ritualistTenshi = new NPC(this,19, "Ritualist Tenshi", 50, 52, 10000, 10000, 10000, 10000,  "A mysteriously cloaked figure with a vast knowledge of dark magic.", tenshiRooms, 11, 1, CloneLootList(lootList), "I will teach you the ways of dark magic",true, 10);
    ritualistTenshi.AvailableSpells["Dark Pact"] = (Description: "Enters you into a temporarily binding contract with an unknown entity, decreasing your health regeneration and increasing your mana regeneration", RequiredLevel: 2, RequiredSkill: "Occultism", RequiredSkillValue: 1, 10);
    ritualistTenshi.AvailableSpells["Blood Pact"] = (Description: "Temporarily enters you into a blood pact with your target, draining your health to heal them over time", RequiredLevel: 2, RequiredSkill: "Blood Magic", RequiredSkillValue: 1, 30);




    _npcs.Add(ritualistTenshi);
    // Add the NPCs to their starting room's NPCsInRoom property
    Room startingRoom1 = GetRoomById(movingNPC.StartingRoomId);
    if (startingRoom1 != null)
    {
        startingRoom1.NPCsInRoom.Add(movingNPC);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{movingNPC.Name}' to room with ID {movingNPC.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => movingNPC.MoveBetweenRooms(this));

    Room startingRoom2 = GetRoomById(sorceryBoundSentinel.StartingRoomId);
    if (startingRoom2 != null)
    {
        startingRoom2.NPCsInRoom.Add(sorceryBoundSentinel);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{sorceryBoundSentinel.Name}' to room with ID {sorceryBoundSentinel.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => sorceryBoundSentinel.MoveBetweenRooms(this));
    
    Room startingRoom4 = GetRoomById(sorceryBoundSentinel4.StartingRoomId);
    if (startingRoom4 != null)
    {
        startingRoom4.NPCsInRoom.Add(sorceryBoundSentinel4);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{sorceryBoundSentinel4.Name}' to room with ID {sorceryBoundSentinel4.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => sorceryBoundSentinel4.MoveBetweenRooms(this));
    
    Room startingRoom5 = GetRoomById(sorceryBoundSentinel5.StartingRoomId);
    if (startingRoom5 != null)
    {
        startingRoom5.NPCsInRoom.Add(sorceryBoundSentinel5);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{sorceryBoundSentinel5.Name}' to room with ID {sorceryBoundSentinel5.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => sorceryBoundSentinel5.MoveBetweenRooms(this));
    Room startingRoom6 = GetRoomById(sorceryBoundSentinel6.StartingRoomId);
    if (startingRoom6 != null)
    {
        startingRoom6.NPCsInRoom.Add(sorceryBoundSentinel6);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{sorceryBoundSentinel6.Name}' to room with ID {sorceryBoundSentinel6.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => sorceryBoundSentinel6.MoveBetweenRooms(this));
    
    Room startingRoom7 = GetRoomById(sorceryBoundSentinel7.StartingRoomId);
    if (startingRoom7 != null)
    {
        startingRoom7.NPCsInRoom.Add(sorceryBoundSentinel7);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{sorceryBoundSentinel7.Name}' to room with ID {sorceryBoundSentinel7.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => sorceryBoundSentinel7.MoveBetweenRooms(this));
    

    
    //NPC
    Room startingRoom8 = GetRoomById(saquiaThrall1.StartingRoomId);
    if (startingRoom8 != null)
    {
        startingRoom8.NPCsInRoom.Add(saquiaThrall1);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaThrall1.Name}' to room with ID {saquiaThrall1.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaThrall1.MoveBetweenRooms(this));
    
    //NPC
    Room startingRoom9 = GetRoomById(saquiaThrall2.StartingRoomId);
    if (startingRoom9 != null)
    {
        startingRoom9.NPCsInRoom.Add(saquiaThrall2);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaThrall2.Name}' to room with ID {saquiaThrall2.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaThrall2.MoveBetweenRooms(this));

    //NPC
    Room startingRoom10 = GetRoomById(saquiaThrall3.StartingRoomId);
    if (startingRoom10 != null)
    {
        startingRoom10.NPCsInRoom.Add(saquiaThrall3);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaThrall3.Name}' to room with ID {saquiaThrall3.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaThrall3.MoveBetweenRooms(this));

    
    //NPC
    Room startingRoom11 = GetRoomById(saquiaThrall4.StartingRoomId);
    if (startingRoom11 != null)
    {
        startingRoom11.NPCsInRoom.Add(saquiaThrall4);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaThrall4.Name}' to room with ID {saquiaThrall4.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaThrall4.MoveBetweenRooms(this));
    //NPC
    Room startingRoom12 = GetRoomById(saquiaThrall5.StartingRoomId);
    if (startingRoom12 != null)
    {
        startingRoom12.NPCsInRoom.Add(saquiaThrall5);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaThrall5.Name}' to room with ID {saquiaThrall5.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaThrall5.MoveBetweenRooms(this));

        //NPC
    Room startingRoom13 = GetRoomById(saquiaSiren1.StartingRoomId);
    if (startingRoom13 != null)
    {
        startingRoom13.NPCsInRoom.Add(saquiaSiren1);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaSiren1.Name}' to room with ID {saquiaSiren1.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaSiren1.MoveBetweenRooms(this));
    //NPC
    Room startingRoom14 = GetRoomById(saquiaSiren2.StartingRoomId);
    if (startingRoom14 != null)
    {
        startingRoom14.NPCsInRoom.Add(saquiaSiren2);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaSiren2.Name}' to room with ID {saquiaSiren2.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaSiren2.MoveBetweenRooms(this));

    //NPC
    Room startingRoom15 = GetRoomById(saquiaSiren3.StartingRoomId);
    if (startingRoom15 != null)
    {
        startingRoom15.NPCsInRoom.Add(saquiaSiren3);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaSiren3.Name}' to room with ID {saquiaSiren3.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaSiren3.MoveBetweenRooms(this));

    
    //NPC
    Room startingRoom16 = GetRoomById(saquiaSiren4.StartingRoomId);
    if (startingRoom16 != null)
    {
        startingRoom16.NPCsInRoom.Add(saquiaSiren4);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaSiren4.Name}' to room with ID {saquiaSiren4.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaSiren4.MoveBetweenRooms(this));
    //NPC
    Room startingRoom17 = GetRoomById(saquiaSiren5.StartingRoomId);
    if (startingRoom17 != null)
    {
        startingRoom17.NPCsInRoom.Add(saquiaSiren5);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{saquiaSiren5.Name}' to room with ID {saquiaSiren5.StartingRoomId}");
    }
    // Start the NPC movement task
    Task.Run(() => saquiaSiren5.MoveBetweenRooms(this));
    //NPC
    Room startingRoom18 = GetRoomById(bathingSiren1.StartingRoomId);
    if (startingRoom18 != null)
    {
        startingRoom18.NPCsInRoom.Add(bathingSiren1);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{bathingSiren1.Name}' to room with ID {bathingSiren1.StartingRoomId}");
    }
    //NPC
    Room startingRoom19 = GetRoomById(bathingSiren2.StartingRoomId);
    if (startingRoom19 != null)
    {
        startingRoom19.NPCsInRoom.Add(bathingSiren2);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{bathingSiren2.Name}' to room with ID {bathingSiren2.StartingRoomId}");
    }
    //NPC
    Room startingRoom20 = GetRoomById(bathingSiren3.StartingRoomId);
    if (startingRoom20 != null)
    {
        startingRoom20.NPCsInRoom.Add(bathingSiren3);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{bathingSiren3.Name}' to room with ID {bathingSiren3.StartingRoomId}");
    }
    //NPC
    Room startingRoom24 = GetRoomById(ritualistTenshi.StartingRoomId);
    if (startingRoom24 != null)
    {
        startingRoom24.NPCsInRoom.Add(ritualistTenshi);
    }
    else
    {
        Console.WriteLine($"Failed to add NPC '{ritualistTenshi.Name}' to room with ID {ritualistTenshi.StartingRoomId}");
    }

    
    _highestNPCId = _npcs.Max(npc => npc.ID); // Set the highest NPC ID after initializing server NPCs

}


        public void SendAnnouncement(string message)
        {
            // Send the message to all connected clients
            foreach (ClientConnection client in _clients)
            {
                client.SendMessage(message);
            }
        }
        private void InitializeDatabase()
        {
            using (SQLiteConnection connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText =
                        "CREATE TABLE IF NOT EXISTS Souls (Id INTEGER PRIMARY KEY, Name TEXT, PasswordHash TEXT, Race TEXT, Age INTEGER, Equipment TEXT, Fashion TEXT, CurrentRoom TEXT, IsGM INTEGER)";

                    command.ExecuteNonQuery();
                }
            }
        }
                
                public void EnterRoom(Soul soul, Room room)
                {
                    room.AddPlayerToRoom(soul, this, room);

                    // Send the announcement message to all players in the room
                    room.SendMessageToAllPlayers($"{soul.Name} has entered the room.", soul);
                }

                public Dictionary<int, Item> CloneLootList(Dictionary<int, Item> originalLootList)
                {
                    Dictionary<int, Item> clonedLootList = new Dictionary<int, Item>();

                    foreach (KeyValuePair<int, Item> kvp in originalLootList)
                    {
                        clonedLootList.Add(kvp.Key, kvp.Value.Clone());
                    }

                    return clonedLootList;
                }

        public void BroadcastMessage(string message)
        {
            foreach (ClientConnection client in _clients)
            {
                client.SendMessage(message);
            }
        }

        public List<ClientConnection> GetConnectedClients()
        {
            lock (_clientsLock)
            {
                return new List<ClientConnection>(_clients);
            }
        }

        private List<Room> _rooms;
        private Room _startingRoom;

        public Room GetRoomById(int roomId)
        {
            var room = _rooms.FirstOrDefault(r => r.ID == roomId);
    
            if (room == null)
            {
                Console.WriteLine($"GetRoomById: Room with ID {roomId} not found.");
                return null;
            }
    
            Console.WriteLine($"GetRoomById: Found room with ID {roomId}.");
            return room;
        }



        public Room GetStartingRoom()
        {
            if (_startingRoom == null)
            {
                _startingRoom = GetRoomById(27);
            }

            return _startingRoom;

        }

        public void RemoveClient(ClientConnection client)
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }
        }

public void SaveSoul(Soul soul)
{
  //  Console.WriteLine($"Saving soul: {soul.Name}, PasswordHash: {soul.PasswordHash}, Salt: {soul.Salt}");
    using (var connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
    {
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
            UPDATE Souls
            SET
                Race = @Race,
                Age = @Age,
                Level = @Level,
                CurrentRoomId = @CurrentRoomId,
                IsGM = @IsGM,
                Astrals = @Astrals,
                VoidAstrals = @VoidAstrals,
                Impcoins = @Impcoins,
                Inventory = @Inventory,
                Equipment = @Equipment,
                ClassName = @ClassName,
                CurrentHP = @CurrentHP,
                CurrentMana = @CurrentMana,
                MaxHP = @MaxHP,
                MaxMana = @MaxMana,
                CurrentExp = @CurrentExp,
                MaxExp = @MaxExp,
                LastLogin = @LastLogin,
                Bio = @Bio,
                Skills = @Skills,
                ClaimedGifts = @ClaimedGifts,
                Fashion = @Fashion,
                LearnedAbilities = @LearnedAbilities,
                Wounds = @Wounds



            WHERE Name = @Name";
            command.Parameters.AddWithValue("@Name", soul.Name);
            command.Parameters.AddWithValue("@Race", soul.Race.RaceType.ToString());
            command.Parameters.AddWithValue("@Age", soul.Age);
            command.Parameters.AddWithValue("@Level", soul.Level);
            command.Parameters.AddWithValue("@CurrentRoomId", soul.CurrentRoomId);
            command.Parameters.AddWithValue("@IsGM", soul.IsGM);
            command.Parameters.AddWithValue("@Astrals", soul.Astrals);
            command.Parameters.AddWithValue("@VoidAstrals", soul.VoidAstrals);
            command.Parameters.AddWithValue("@Impcoins", soul.Impcoins);
            command.Parameters.AddWithValue("@Inventory", JsonConvert.SerializeObject(soul.Inventory));
            command.Parameters.AddWithValue("@Equipment", JsonConvert.SerializeObject(soul.Equipment));
            command.Parameters.AddWithValue("@ClassName", soul.CurrentClass.Name);
            command.Parameters.AddWithValue("@CurrentHP", soul.CurrentHealth);
            command.Parameters.AddWithValue("@CurrentMana", soul.CurrentMana);
            command.Parameters.AddWithValue("@MaxHP", soul.MaxHealth);
            command.Parameters.AddWithValue("@MaxMana", soul.MaxMana);
            command.Parameters.AddWithValue("@CurrentExp", soul.CurrentExperience);
            command.Parameters.AddWithValue("@MaxExp", soul.MaxExperience);
            command.Parameters.AddWithValue("@LastLogin", soul.LastLogin);
            command.Parameters.AddWithValue("@Bio", soul.Bio);
            command.Parameters.AddWithValue("@Skills", JsonConvert.SerializeObject(soul.Skills));
            command.Parameters.AddWithValue("@RecallRoom", soul.RecallRoom);
            command.Parameters.AddWithValue("@ClaimedGifts", JsonConvert.SerializeObject(soul.ClaimedGifts));
            command.Parameters.AddWithValue("@Fashion", JsonConvert.SerializeObject(soul.Fashion));
            command.Parameters.AddWithValue("@LearnedAbilities", JsonConvert.SerializeObject(soul.LearnedAbilities));
            command.Parameters.AddWithValue("@Wounds", JsonConvert.SerializeObject(soul.Wounds));
            command.ExecuteNonQuery();
        }
    }
}



public async Task<Soul> LoadSoul(string soulName, string password)
{
    Soul loadedSoul = null;
    using (var connection = new SQLiteConnection("Data Source=SanctuaryMUD.db"))
    {
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Souls WHERE LOWER(Name) = LOWER(@Name)";
            command.Parameters.AddWithValue("@Name", soulName);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    string storedSalt = reader.GetString(reader.GetOrdinal("Salt"));
                    string storedPasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                    string inputPasswordHash = GetPasswordHash(password, storedSalt);
                    if (string.Equals(inputPasswordHash, storedPasswordHash, StringComparison.OrdinalIgnoreCase))
                    {
                        string raceString = reader["Race"].ToString();

                        if (Enum.TryParse<RaceType>(raceString, true, out var raceType))
                        {
                            Race race = Races.GetRace(raceType);
                            string name = reader["Name"].ToString();
                            string passwordHash = reader["PasswordHash"].ToString();
                            string salt = reader["Salt"].ToString();
                            int age = Convert.ToInt32(reader["Age"]);
                            int level = Convert.ToInt32(reader["Level"]);
                            int astrals = Convert.ToInt32(reader["Astrals"]);
                            int currentRoomId = Convert.ToInt32(reader["CurrentRoomId"]);
                            int isGMColumnIndex = -1;
                            bool isGM = false;
                            var recallRoom = reader.GetInt32(25);
                            try
                            {
                                isGMColumnIndex = reader.GetOrdinal("IsGM");
                                isGM = reader.GetInt32(isGMColumnIndex) == 1;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error retrieving IsGM value: {ex.Message}");
                            }
                            int impcoins = reader.GetInt32(reader.GetOrdinal("Impcoins"));
                            // Load the skills from the database
                            string skillsJson = reader["Skills"].ToString();
                            Dictionary<string, Skill> skills = JsonConvert.DeserializeObject<Dictionary<string, Skill>>(skillsJson);
                            // Load the LastLogin from the database
                            DateTime lastLogin;
                            string lastLoginString = reader["LastLogin"].ToString();
                            if (string.IsNullOrEmpty(lastLoginString))
                            {
                                lastLogin = DateTime.UtcNow;
                            }
                            else
                            {
                                lastLogin = DateTime.Parse(lastLoginString);
                            }
                            string bio = reader["Bio"].ToString();
                            loadedSoul = new Soul(this, name, passwordHash, race, salt, age,
                                race.MaxHealth, race.MaxMana, race.MaxHealth, race.MaxMana, impcoins, level,
                                currentRoomId, isGM, lastLogin, bio, skills, recallRoom, astrals);
                            //load the learnedabilities from the database
                            string learnedAbilitiesJson = reader["LearnedAbilities"].ToString();
                            List<string> learnedAbilities = JsonConvert.DeserializeObject<List<string>>(learnedAbilitiesJson);
                            loadedSoul.LearnedAbilities = learnedAbilities;
                            // Load the class from the database
                            loadedSoul.LoadClassFromDatabase(connection);
                            // Add class abilities to the soul
                            if (loadedSoul.CurrentClass != null)
                            {
                                loadedSoul.Abilities.AddRange(loadedSoul.CurrentClass.Abilities);
                            }
                            // Load the inventory from the database
                            string inventoryJson = reader["Inventory"].ToString();
                            if (!string.IsNullOrEmpty(inventoryJson))
                            {
                                Dictionary<int, Item> inventory = JsonConvert.DeserializeObject<Dictionary<int, Item>>(inventoryJson);
                                // Debug messages to check the deserialized items
                                foreach (var item in inventory)
                                {
                                    Console.WriteLine($"Item Key: {item.Key}, Item ID: {item.Value.ID}, Item Name: {item.Value.Name}");
                                }
                                loadedSoul.Inventory = inventory;
                            }
                            // Load the equipment from the database
                            string equipmentJson = reader["Equipment"].ToString();
                            if (!string.IsNullOrEmpty(equipmentJson))
                            {
                                Dictionary<int, Item> equipment = JsonConvert.DeserializeObject<Dictionary<int, Item>>(equipmentJson);

                                // Debug messages to check the deserialized equipment
                                foreach (var slotItem in equipment)
                                {
                                    Console.WriteLine($"Slot Key: {slotItem.Key}, Item ID: {slotItem.Value?.ID}, Item Name: {slotItem.Value?.Name}");
                                }
                                loadedSoul.Equipment = equipment;
                            }
                            // Load the fashion inventory from the database
                            string fashionJson = reader["Fashion"].ToString();
                            if (!string.IsNullOrEmpty(fashionJson))
                            {
                                Dictionary<string, Item> fashion = JsonConvert.DeserializeObject<Dictionary<string, Item>>(fashionJson);
                                foreach (var slotItem in fashion)
                                {
                                    Console.WriteLine($"Slot Key: {slotItem.Key}, Item ID: {slotItem.Value?.ID}, Item Name: {slotItem.Value?.Name}");
                                }
                                loadedSoul.Fashion = fashion;
                            }
                            else
                            {
                                loadedSoul.Fashion = new Dictionary<string, Item>(); 
                            }
                            // load the wounds from the database
                            string woundsJson = reader["Wounds"].ToString();
                            if (!string.IsNullOrEmpty(woundsJson))
                            {
                                List<string> wounds = JsonConvert.DeserializeObject<List<string>>(woundsJson);
                                loadedSoul.Wounds = wounds;
                            }
                            else
                            {
                                loadedSoul.Wounds = new List<string>(); 
                            }
                            // Check if the ClaimedGifts column exists and if its value is not null
                            int claimedGiftsColumnIndex = -1;
                            string claimedGiftsJson = string.Empty;
                            try
                            {
                                claimedGiftsColumnIndex = reader.GetOrdinal("ClaimedGifts");
                                if (!reader.IsDBNull(claimedGiftsColumnIndex))
                                {
                                    claimedGiftsJson = reader["ClaimedGifts"].ToString();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error retrieving ClaimedGifts value: {ex.Message}");
                            }

                            if (!string.IsNullOrEmpty(claimedGiftsJson))
                            {
                                Dictionary<string, bool> claimedGifts = JsonConvert.DeserializeObject<Dictionary<string, bool>>(claimedGiftsJson);
                                foreach (var claimedGift in claimedGifts)
                                {
                                    Console.WriteLine($"Gift Code: {claimedGift.Key}, Claimed: {claimedGift.Value}");
                                }
                                loadedSoul.ClaimedGifts = claimedGifts;
                            }
                            else
                            {
                                loadedSoul.ClaimedGifts = new Dictionary<string, bool>(); 
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to read from the reader or no matching soul found.");
                    }
                    }
                else
                {
                    Console.WriteLine($"Failed to read from the reader or no matching soul found.");
                }
            }
        }
    }
    return loadedSoul;
}

public Soul GetSoulByName(string name)
{
    foreach (Soul soul in Souls)
    {
        if (soul.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return soul;
        }
    }

    return null;
}


public async Task ProcessCorpseStates()
{
    while (true)
    {
        foreach (var room in _rooms)
        {
            for (int i = room.NPCsInRoom.Count - 1; i >= 0; i--)
            {
                NPC npc = room.NPCsInRoom[i];
                if (npc.IsDead && npc.HasBeenLooted)
                {
                    TimeSpan timeSinceLooted = DateTime.UtcNow - npc.TimeLooted;

                    if (timeSinceLooted.TotalMinutes >= 35)
                    {
                        room.NPCsInRoom.RemoveAt(i);
                    }
                    else if (timeSinceLooted.TotalMinutes >= 30)
                    {
                        npc.Status = "Disintegrating";
                    }
                    else if (timeSinceLooted.TotalMinutes >= 20)
                    {
                        npc.Status = "Stinking";
                    }
                    else if (timeSinceLooted.TotalMinutes >= 10)
                    {
                        npc.Status = "Rotting";
                    }
                    else
                    {
                        npc.Status = "Decomposing";
                    }
                }
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(60)); // Update corpse states every 60 seconds.
    }
}



public void CreateSampleItems(Soul soul)
{
        if (soul.Inventory == null)
    {
        Console.WriteLine("soul.Inventory is null!");
        return;
    }
        List<Item> sampleItems = new List<Item>
        {
            ItemManager.GetItem(1), // Flask
            ItemManager.GetItem(2), // Torch
            ItemManager.GetItem(3), // Sword
            ItemManager.GetItem(4)  // Leather Boots
            // ... Add more items as needed ...
        };


    int maxInventorySlots = 15; // Set your desired maximum inventory slots

    foreach (Item item in sampleItems)
    {
        for (int i = 1; i <= maxInventorySlots; i++)
        {
            if (!soul.Inventory.ContainsKey(i))
            {
                soul.Inventory[i] = item;
                break;
            }
        }
    }
}

  

        public static string GetPasswordHash(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var saltBytes = Convert.FromBase64String(salt); // Convert salt from Base64 string to byte array
                Console.WriteLine($"Salt bytes: {BitConverter.ToString(saltBytes).Replace("-", "")}");

                var passwordWithSalt = Encoding.UTF8.GetBytes(password).Concat(saltBytes).ToArray(); // Concatenate password and salt bytes
                Console.WriteLine($"Password with salt: {BitConverter.ToString(passwordWithSalt).Replace("-", "")}");

                var hashedBytes = sha256.ComputeHash(passwordWithSalt);
                Console.WriteLine($"Hashed bytes: {BitConverter.ToString(hashedBytes).Replace("-", "")}");

                return BitConverter.ToString(hashedBytes).Replace("-", ""); // Remove .ToLower()
            }
        }




        public static async Task Main(string[] args)
        {
            int port = 4000;
            Server server = new Server(port);
            Task serverTask = server.Start();

            Console.WriteLine("Press any key to stop the server...");
            Task keyPressTask = Task.Run(() => Console.ReadKey(true));

            await Task.WhenAny(serverTask, keyPressTask);
        }
    }
}
