namespace SanctuaryMUD
{
    public class NPC : Entity
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int StartingRoomId { get; set; }
        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }
        public Dictionary<Soul, int> ThreatLevels { get; set; } = new Dictionary<Soul, int>();
        public int CurrentHealth { get; set; }
        public int CurrentMana { get; set; }
        public string Description { get; set; }
        public List<int> AllowedRooms { get; set; }
        public Dictionary<int, Item> LootList { get; set; }
        public int CurrentRoomId { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsDead { get; set; }
        public bool IsHidden { get; set; }
        public Entity CurrentTarget { get; set; }
        public string DelayedDialogue { get; set; }
        public int DelayedDialogueTime { get; set; }
        public int AttackSpeed { get; set; }
        public int AttackDamage { get; set; }
        public bool IsMerchant { get; set; }
        public bool IsTrainer { get; set; }
        public bool IsPetrified { get; set; }

        public string Dialogue { get; set; }
        public bool HasBeenLooted { get; set; }
        public DateTime TimeLooted { get; set; }
        public string Status { get; set; }
        public string DecompositionStatus { get; set; }
        private Server _server;
        public System.Timers.Timer DecompositionTimer { get; set; }
        public int RespawnCounter { get; set; } 
        public int RespawnTimer { get; set; }
        public string OriginalName { get; set; }
        public bool HasBeenDefiled { get; set; } = false;
        public bool HasBeenAttacked { get; set; } = false;
        private Soul soul;


        public Dictionary<string, (string Description, int RequiredLevel, string RequiredSkill, int RequiredSkillValue, int AstralCost)>
            AvailableSpells { get; set; }

        public NPC(Server server, int id, string name, int level, int startingRoomId, int maxHealth, int maxMana,
            int currentHealth, int currentMana, string description, List<int> allowedRooms, int attackSpeed,
            int attackDamage, Dictionary<int, Item> lootList, string dialogue, bool isTrainer, int respawnTimer, int respawnCounter = 0, int delayedDialogueTime = 0,
            string delayedDialogue = "")
        {
            ID = id;
            Name = name;
            Level = level;
            StartingRoomId = startingRoomId;
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            CurrentHealth = currentHealth;
            CurrentMana = currentMana;
            Description = description;
            AllowedRooms = allowedRooms;
            IsAttacking = false;
            IsDead = false;
            IsHidden = false;
            CurrentTarget = null;
            AttackSpeed = attackSpeed;
            AttackDamage = attackDamage;
            LootList = lootList ?? new Dictionary<int, Item>();
            IsTrainer = isTrainer;
            AvailableSpells =
                new Dictionary<string, (string Description, int RequiredLevel, string RequiredSkill, int RequiredSkillValue, int AstralCost
                    )>();
            Dialogue = dialogue;
            HasBeenLooted = false;
            RespawnTimer = respawnTimer;
            RespawnCounter = respawnCounter;
            _server = server;
            DelayedDialogueTime = delayedDialogueTime;
            DelayedDialogue = delayedDialogue;


            // Update the Name property to include the respawn counter if greater than 0
            if (RespawnCounter > 0)
            {
                Name = $"{Name} {RespawnCounter}";
            }
        }
        public void UpdateThreat(Soul attacker, int damage)
        {
            if (ThreatLevels.ContainsKey(attacker))
            {
                ThreatLevels[attacker] += damage;
            }
            else
            {
                ThreatLevels.Add(attacker, damage);
            }
        }
        public Soul GetHighestThreatTarget()
        {
            if (ThreatLevels.Count == 0)
            {
                return null;
            }

            KeyValuePair<Soul, int> highestThreat = ThreatLevels.OrderByDescending(t => t.Value).First();
            return highestThreat.Key;
        }

        public void StartDecompositionTimer(Server server)
        {
            DecompositionTimer = new System.Timers.Timer();
            DecompositionTimer.Interval = 100 * 60 * 10; // 1 minute in milliseconds
            DecompositionTimer.Elapsed += (sender, args) => OnDecompositionTimerElapsed(server);
            DecompositionTimer.AutoReset = false;
            DecompositionTimer.Enabled = true;

            Console.WriteLine($"Decomposition timer started for NPC {Name}"); // Debug message
        }
        
        public async Task TriggerDelayedDialogue(Soul soul, Server server)
        {
            if (DelayedDialogueTime > 0 && !string.IsNullOrEmpty(DelayedDialogue))
            {
                await Task.Delay(DelayedDialogueTime * 1000); // Convert seconds to milliseconds
                soul.Connection.SendMessage(DelayedDialogue);
            }
        }

        private async void OnDecompositionTimerElapsed(Server server)
        {
            Console.WriteLine($"Decomposition timer elapsed for NPC {Name}, current status: {Status}"); // Debug message

            if (Status == "Decomposing")
            {
                Status = "Rotting";
                DecompositionTimer.Interval = 300 * 60 * 10; // 3 minutes in milliseconds
                DecompositionTimer.Enabled = true;
            }
            else if (Status == "Rotting")
            {
                Status = "Stinking";
                DecompositionTimer.Interval = 100 * 60 * 10; // 1 minute in milliseconds
                DecompositionTimer.Enabled = true;
            }
            else if (Status == "Stinking")
            {
                Status = "Disintegrating";
                DecompositionTimer.Interval = 100 * 60 * 5; // 30 seconds minutes in milliseconds
                DecompositionTimer.Enabled = true;
            }
            else if (Status == "Disintegrating")
            {
                // Remove the NPC from the room
                Room npcRoom = server.GetRoomById(CurrentRoomId);
                if (npcRoom != null)
                {
                    npcRoom.NPCsInRoom.Remove(this);
                }
            }
        }


        public async Task MoveBetweenRooms(Server server)
{
    try
    {
        // Make sure the NPC is not in IsAttacking mode
        if (IsAttacking)
        {
            Console.WriteLine($"NPC {ID} is attacking and cannot move between rooms.");
            return;
        }

        CurrentRoomId = StartingRoomId;
        Room currentRoom = server.GetRoomById(CurrentRoomId);
        if (currentRoom == null)
        {
            Console.WriteLine($"NPC {ID} could not find starting room with ID {StartingRoomId}.");
            return;
        }

        Console.WriteLine($"NPC {ID} is starting to move between rooms.");

        // Start monitoring the attacking state
        _ = MonitorAttackingState(server);

        Random rand = new Random();
        while (true)
        {
            int minDelay = 60; // Minimum delay in seconds
            int maxDelay = 600; // Maximum delay in seconds
            int delay = rand.Next(minDelay, maxDelay + 1); // Generate random delay between minDelay and maxDelay (inclusive)

            await Task.Delay(TimeSpan.FromSeconds(delay)); // Wait for the random delay before moving

            // Check if the NPC is still in IsAttacking mode
            if (IsAttacking)
            {
                Console.WriteLine($"NPC {ID} is attacking and cannot leave the room.");
                continue;
            }

            // Check if the NPC is dead
            if (IsDead)
            {
                Console.WriteLine($"NPC {ID} is dead and cannot change rooms.");
                return;
            }

            List<Room> connectedRooms = currentRoom.GetConnectedRooms(server);
            connectedRooms.RemoveAll(r => r.ID == currentRoom.ID); // Remove current room from connected rooms list
            if (connectedRooms.Count == 0)
            {
                Console.WriteLine($"NPC {ID} is in the only allowed room and cannot move.");
                continue;
            }

            Room nextRoom = connectedRooms.FirstOrDefault(r => AllowedRooms.Contains(r.ID) && !r.NPCs.Any(n => n.Name == this.Name && n.ID != this.ID));

            if (nextRoom != null)
            {
                Console.WriteLine($"NPC {ID} is moving from room {currentRoom.ID} to room {nextRoom.ID}.");

                // Check if the NPC is dead before removing from the current room
                if (!IsDead)
                {
                    await currentRoom.RemoveNPC(this, server);
                }

                // Check if the NPC is dead before adding to the next room
                if (!IsDead)
                {
                    await nextRoom.AddNPC(this, server);
                }

                currentRoom = nextRoom;
                CurrentRoomId = currentRoom.ID;
            }
            else
            {
                Console.WriteLine(
                    $"NPC {ID} could not find any allowed rooms to move to from room {currentRoom.ID}.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"NPC {ID} encountered an error while moving between rooms: {ex.Message}");
    }
}




        public async Task AttackLoop(Server server)
        {
            while (IsAttacking && !IsDead && CurrentTarget != null)
            {
                int interval = CalculateAttackInterval(AttackSpeed, Level);
                await Task.Delay(TimeSpan.FromSeconds(interval));

                // Add this line to check if the NPC is petrified
                if (IsPetrified) continue;

                // Check if the NPC's health has reached 0
                if (CurrentHealth <= 0)
                {
                    IsDead = true;
                    IsAttacking = false;
                    Room currentRoom = server.GetRoomById(CurrentRoomId);
                    if (currentRoom != null)
                    {
                        // Define a list of death messages
                        List<string> deathMessages = new List<string>
                        {
                           $"{Name} takes their final breath, their spirit soaring beyond the mortal realm.",
$"{Name} closes their eyes for the last time, leaving behind memories that will never fade.",
$"In their final moments, {Name} is surrounded by loved ones, who mourn their passing.",
$"As {Name} leaves this world, their legacy lives on in the hearts of those they touched.",
$"The world mourns the loss of {Name}, a shining light extinguished too soon.",
$"In death, {Name} finds release from the pain and suffering of life.",
$"As {Name} slips away, they feel a sense of wonder at the mysteries that await them.",
$"{Name} takes their final bow, leaving behind a life well-lived and a world forever changed.",
$"In the end, {Name} finds solace in the knowledge that they made a difference.",
$"{Name} embraces death with grace and dignity, at peace with what lies ahead.",
$"As {Name} draws their last breath, they are filled with gratitude for the blessings of life.",
$"The world is a darker place without {Name}, whose light has been extinguished.",
$"In death, {Name} is reunited with loved ones who have gone before.",
$"{Name} slips away quietly, leaving behind a world that will never forget them.",
$"As the light fades from {Name}'s eyes, they know that their spirit will live on forever.",
$"The final moments of {Name}'s life are filled with memories of a life well-lived.",
$"{Name} falls into eternal slumber, their journey in this life complete.",
$"In their final breaths, {Name} finds comfort in the knowledge that they are loved.",
$"As {Name} crosses over into the next realm, they are greeted by a chorus of angels.",
$"The world weeps for {Name}, whose presence will be sorely missed.",
$"In death, {Name} becomes a part of something greater than themselves.",
$"As {Name} fades away, their spirit is lifted by the love of those they leave behind.",
$"The last thing {Name} hears is the sound of loved ones saying goodbye.",
$"In their final moments, {Name} knows that their spirit will live on forever.",
$"As {Name} takes their final breath, they are at peace with the universe.",
$"{Name} passes away, leaving behind a legacy that will never be forgotten.",
$"In death, {Name} discovers the secrets of the universe and finds peace.",
$"As the light fades from {Name}'s eyes, they find comfort in the knowledge that they lived a good life.",
$"The world mourns the loss of {Name}, whose light shone brightly while they were alive.",
$"In their final moments, {Name} is surrounded by love and warmth.",
$"{Name} takes their final journey, leaving behind a world that will never be the same.",
$"As {Name} slips away, they are embraced by the love of those they leave behind.",
$"The world is a better place for having had {Name} in it, even if only for a little while.",
$"In death, {Name} is reunited with loved ones and friends who have gone before.",
$"As {Name} draws their last breath, they are at peace with the world.",
$"The last thing {Name} feels is the warmth of the sun on their face.", 
$"{Name} leaves this world, knowing that they have left a positive impact on those around them.",
$"{Name} departs with a sense of fulfillment, having lived a life true to themselves.",
$"In their final moments, {Name} is surrounded by the beauty of nature, finding peace in the natural world.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through their art.",
$"As {Name} takes their final breath, they find solace in the thought of being reunited with their beloved pets.",
$"{Name} passes away with a smile on their face, grateful for the love and support of their family and friends.",
$"In death, {Name} finds the answers to the mysteries that eluded them in life.",
$"{Name} slips away peacefully, leaving behind a world that is a little brighter for having known them.",
$"As the light fades from {Name}'s eyes, they are filled with wonder at the beauty of the universe.",
$"The world is a quieter place without {Name}, whose laughter and joy will be sorely missed.",
$"{Name} leaves behind a legacy of kindness and generosity, inspiring those who come after them.",
$"In their final moments, {Name} is embraced by the beauty of the stars, finding comfort in the infinite expanse of the cosmos.",
$"As {Name} takes their final journey, they leave behind a world that is forever changed by their presence.",
$"{Name} passes away, leaving behind memories that will be cherished by all who knew them.",
$"In death, {Name} finds the answers to the questions that plagued them in life.",
$"{Name} slips away, leaving behind a world that is richer for having known them.",
$"As the light fades from {Name}'s eyes, they are filled with a sense of wonder at the interconnectedness of all things.",
$"The world mourns the loss of {Name}, a bright and shining star that has gone out too soon.",
$"{Name} leaves this world with the knowledge that they have made a difference in the lives of others.",
$"In their final moments, {Name} is surrounded by the beauty of nature, finding peace in the cycles of life and death.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through their children.",
$"As {Name} takes their final breath, they find solace in the thought of being reunited with their soulmate.",
$"{Name} passes away with the knowledge that they have lived a life true to their values.",
$"In death, {Name} discovers the interconnectedness of all things, finding peace in the unity of the universe.",
$"{Name} slips away quietly, leaving behind a world that is forever changed by their legacy.",
$"As the light fades from {Name}'s eyes, they are filled with a sense of wonder at the beauty of creation.",
$"The world is a kinder place for having had {Name} in it, even if only for a little while.",
$"{Name} leaves behind a legacy of hope and inspiration, encouraging others to strive for greatness.",
$"In their final moments, {Name} is surrounded by the love and support of their community, finding solace in the bonds of human connection.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through the memories of those they leave behind.",
$"{Name} gasps their last breath, the room filled with a haunting silence.",
$"{Name} stares into the abyss of death, their eyes filled with a cold, empty darkness.",
$"In their final moments, {Name} screams in agony, begging for mercy that will never come.",
$"{Name} falls to the floor, a lifeless shell devoid of all meaning and purpose.",
$"As {Name} takes their final breath, a chill fills the air, as if Death himself has arrived.",
$"{Name} dies alone, their final thoughts filled with regret and despair.",
$"In death, {Name} becomes a hollow, empty vessel, lost in the void of the afterlife.",
$"{Name} is consumed by the darkness of death, their soul doomed to eternal torment.",
$"As {Name} draws their final breath, a sense of dread fills the room, as if something evil is watching from the shadows.",
$"{Name} leaves this world, a trail of destruction and pain in their wake.",
$"In their final moments, {Name} is tormented by visions of a hellish landscape, their fate forever sealed.",
$"{Name} falls to the ground, their body wracked with pain, as they succumb to the icy grip of death.",
$"As {Name} takes their final journey, they are consumed by a darkness that will never fade.",
$"{Name} dies screaming, their voice echoing through the halls of the afterlife.",
$"In death, {Name} is swallowed by a void of eternal nothingness, their spirit lost forever.",
$"{Name} meets their end, a victim of the cruel and merciless hand of fate.",
$"{Name} breathes their last, a shadow of their former self, stripped of all vitality and strength.",
$"As {Name} slips away, a cold, foreboding presence fills the room, as Death claims another soul.",
$"{Name} departs this world, leaving behind a trail of darkness and despair that will never be forgotten.",
$"In their final moments, {Name} is consumed by a sense of futility, as if their life had no meaning or purpose.",
$"{Name} dies alone and forgotten, a mere footnote in the annals of time.",
$"As {Name} takes their final breath, a sense of dread fills the air, as if something malevolent is lurking just beyond the veil.",
$"{Name} is consumed by the fires of hell, their soul forever trapped in a prison of agony and despair.",
$"{Name} falls to the ground, their lifeblood staining the earth, a sacrifice to the dark powers that be.",
$"In death, {Name} is condemned to wander the afterlife, a lost and tormented spirit, forever searching for redemption.",
$"{Name} meets their end with a sense of resignation, knowing that their fate was sealed from the moment they were born.",
$"As {Name} draws their last breath, the very fabric of reality seems to warp and twist, as if the universe itself is mourning their passing.",
$"{Name} leaves this world, a curse upon their lips, a warning to all who dare to follow in their footsteps.",
$"In their final moments, {Name} is consumed by a darkness that blots out all hope and light.",
$"{Name} dies alone and forgotten, their passing unnoticed by a world that had long since moved on.",
$"As {Name} takes their final journey, they are consumed by a sense of regret, knowing that they will never see the light of day again.",
$"{Name} takes their final breath, their eyes filled with terror as they realize the true nature of death.",
$"{Name} dies screaming, their soul ripped apart by forces beyond their comprehension.",
$"In their final moments, {Name} is consumed by a sense of dread, as if something evil is lurking just beyond their field of vision.",
$"{Name} falls to the ground, their body twisted and contorted by the agony of their passing.",
$"As {Name} takes their final breath, the air around them grows thick with a noxious, choking mist.",
$"{Name} dies alone and afraid, their screams echoing through the empty halls of the afterlife.",
$"In death, {Name} is consumed by a darkness that blots out all hope and light, their soul forever trapped in a void of eternal suffering.",
$"{Name} meets their end, consumed by a madness that is beyond comprehension.",
$"As {Name} slips away, a cold, clammy hand reaches out to claim their soul, pulling them down into the abyss of death.",
$"{Name} falls to the ground, their eyes wide with terror, as they are dragged screaming into the jaws of oblivion.",
$"In their final moments, {Name} is consumed by a sense of overwhelming horror, as they realize that they are not alone in the darkness.",
$"{Name} dies alone, their body wracked with pain and terror, as they are dragged down into the pits of hell.",
$"As {Name} takes their final journey, they are pursued by a legion of howling demons, eager to claim their soul for their master.",
$"{Name} is consumed by the fires of hell, their screams echoing through the halls of the damned.",
$"In death, {Name} is lost in a labyrinth of terror and despair, forever tormented by the horrors that await them in the afterlife.",
$"{Name} falls to the ground, their body wracked with convulsions, as they are consumed by a darkness that will never fade.",
$"As {Name} draws their last breath, they are consumed by a sense of overwhelming dread, as they realize that they are about to face judgment for their sins.",
$"{Name} dies alone, their body twisted and contorted by the malevolent forces that claimed their soul.",
$"In their final moments, {Name} is consumed by a sense of overwhelming terror, as they realize that they are being pursued by a malevolent entity that seeks to claim their soul.",
$"{Name} takes their final breath, their eyes filled with terror, as they are dragged screaming into the abyss of death."
                        };
                    }
                    else
                    {
                        Console.WriteLine($"NPC {ID} could not find the room with ID {CurrentRoomId}.");
                    }

                    break;
                }

                if (CurrentTarget is Soul targetSoul)
                {
                    // Check if the player's soul is dead or in a different room
                    if (!targetSoul.IsAlive || targetSoul.CurrentRoomId != CurrentRoomId)
                    {
                        IsAttacking = false;
                        break;
                    }
                    
                    // Get the target with the highest threat level
                    Soul highestThreatTarget = GetHighestThreatTarget();

                    // If no valid target is found, stop attacking
                    if (highestThreatTarget == null)
                    {
                        IsAttacking = false;
                        break;
                    }

                    // Set the current target to the highest threat target
                    CurrentTarget = highestThreatTarget;

                    // Check if the player is a moon elf and roll for the 2% dodge chance
                    bool dodged = false;
                    if (targetSoul.Race.RaceName.Equals("moon elf", StringComparison.OrdinalIgnoreCase))
                    {
                        Random random = new Random();
                        int dodgeRoll = random.Next(1, 101);
                        if (dodgeRoll <= 2)
                        {
                            dodged = true;
                            targetSoul.Connection.SendMessage("You dodged the attack!\r\n");
                        }
                    }

// If the attack was not dodged, proceed with the damage calculation
                    if (!dodged)
                    {
                        int damage = CalculateDamage(AttackDamage, Level);
                        int actualDamage = Math.Min(damage, targetSoul.CurrentHealth);
                        targetSoul.CurrentHealth -= actualDamage;

                        // Update the threat level for the attacking NPC
                        UpdateThreat(targetSoul, actualDamage);

                        targetSoul.Connection.SendMessage($"{Name} hits you for {actualDamage} damage!\r\n");
                        targetSoul.Connection.SendMessage(
                            $"HP: {ClientConnection.GenerateProgressBar(targetSoul.CurrentHealth, targetSoul.MaxHealth, 20, ConsoleColor.Red)} ({targetSoul.CurrentHealth}/{targetSoul.MaxHealth})\r\n"
                        );
                    


                        // Check if the NPC's health has reached 0
                        if (CurrentHealth <= 0)
                        {
                            IsDead = true;
                            IsAttacking = false;
                           

                            // Check if the target player's health has reached 0
                            if (targetSoul.CurrentHealth <= 0)
                            {
                                targetSoul.IsAlive = false; // Set IsDead flag to true
                                targetSoul.HandlePlayerDeath(server);
                                targetSoul.Connection.SendMessage($"You have died!\r\n");
                            }
                        }
                    }
                }
            }
        }



        private int CalculateDamage(int attackDamage, int level)
        {
            // Example formula: damage = attackDamage + level * 0.5
            int damage = (int)(attackDamage + level * 0.5);
            return damage;
        }

        private int CalculateAttackInterval(int attackSpeed, int level)
        {
            // Implement your formula to calculate the attack interval based on attackSpeed and level
            // For example, you can use the following formula:
            int interval = (int)(attackSpeed - (0.1 * level));

            // Make sure the interval is greater than or equal to 1 second
            return Math.Max(1, interval);
        }

        public async Task MonitorAttackingState(Server server)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)); // Check every second

                if (IsAttacking && CurrentTarget != null)
                {
                    Room currentRoom = server.GetRoomById(CurrentRoomId);

                    if (currentRoom == null)
                    {
                        Console.WriteLine($"NPC {ID} could not find the room with ID {CurrentRoomId}.");
                        IsAttacking = false;
                        continue;
                    }

                    // Check if the target NPC is dead
                    if (CurrentTarget is NPC targetNPC && targetNPC.IsDead)
                    {
                        Console.WriteLine($"NPC {ID}'s target {targetNPC.Name} has died.");
                        IsAttacking = false;
                        CurrentTarget = null;
                        continue;
                    }

                    // Check if the target soul is still in the room
                    if (CurrentTarget is Soul targetSoul && !currentRoom.ContainsSoul(targetSoul))
                    {
                        Console.WriteLine($"NPC {ID} lost sight of target soul {targetSoul.Name}.");
                        IsAttacking = false;
                        CurrentTarget = null;
                        continue;
                    }

                    // Check if the NPC has been attacked by a player
                    if (HasBeenAttacked)
                    {
                        // Find the player who attacked the NPC
                        Soul attacker = currentRoom.PlayersInRoom.FirstOrDefault(s => s.CurrentTarget == this);

                        if (attacker != null)
                        {
                            // Attack the player back
                            Console.WriteLine($"NPC {ID} has been attacked by {attacker.Name}.");
                            IsAttacking = true;
                            CurrentTarget = attacker;

                            // Reset HasBeenAttacked flag
                            HasBeenAttacked = false;
                        }
                    }

                    // Attack loop
                    await AttackLoop(server);
                }
            }
        }
        
        public NPC Clone()
        {
            NPC clonedNPC = (NPC)MemberwiseClone();
            clonedNPC.LootList = new Dictionary<int, Item>();

            foreach (var kvp in LootList)
            {
                clonedNPC.LootList.Add(kvp.Key, kvp.Value.Clone());
            }

            return clonedNPC;
        }

        
        public void DistributeExperienceToPlayer(Soul soul)
        {
            int npcLevel = Level;
            int levelDifference = soul.Level - npcLevel;

            if (levelDifference >= 5)
            {
                soul.Connection.SendMessage("You are too high level to gain experience from this NPC.\r\n");
            }
            else
            {
                float scalingFactor = 1f;

                if (levelDifference > 0)
                {
                    scalingFactor -= Math.Min(0.1f * levelDifference, 0.5f);
                }
                else if (levelDifference < 0)
                {
                    scalingFactor += Math.Min(0.1f * -levelDifference, 0.5f);
                }

                int experienceGain = (int)Math.Round(npcLevel * 10 * scalingFactor);
                experienceGain = Math.Max(0, experienceGain);

                soul.CurrentExperience += experienceGain;
                soul.Connection.SendMessage($"You gained {experienceGain} experience!\r\n");
                
            }
        }

        public void DistributeExperienceToGroup(Group group)
        {
            foreach (Soul member in group.Members)
            {
                DistributeExperienceToPlayer(member);
            }
        }



        public void Die()
        {
            IsDead = true;
            IsAlive = false;
            IsAttacking = false;
            CurrentTarget = null;
            Status = "Decomposing";
// Define a list of death messages
                        List<string> deathMessages = new List<string>
                        {
                           $"{Name} takes their final breath, their spirit soaring beyond the mortal realm.",
$"{Name} closes their eyes for the last time, leaving behind memories that will never fade.",
$"In their final moments, {Name} is surrounded by loved ones, who mourn their passing.",
$"As {Name} leaves this world, their legacy lives on in the hearts of those they touched.",
$"The world mourns the loss of {Name}, a shining light extinguished too soon.",
$"In death, {Name} finds release from the pain and suffering of life.",
$"As {Name} slips away, they feel a sense of wonder at the mysteries that await them.",
$"{Name} takes their final bow, leaving behind a life well-lived and a world forever changed.",
$"In the end, {Name} finds solace in the knowledge that they made a difference.",
$"{Name} embraces death with grace and dignity, at peace with what lies ahead.",
$"As {Name} draws their last breath, they are filled with gratitude for the blessings of life.",
$"The world is a darker place without {Name}, whose light has been extinguished.",
$"In death, {Name} is reunited with loved ones who have gone before.",
$"{Name} slips away quietly, leaving behind a world that will never forget them.",
$"As the light fades from {Name}'s eyes, they know that their spirit will live on forever.",
$"The final moments of {Name}'s life are filled with memories of a life well-lived.",
$"{Name} falls into eternal slumber, their journey in this life complete.",
$"In their final breaths, {Name} finds comfort in the knowledge that they are loved.",
$"As {Name} crosses over into the next realm, they are greeted by a chorus of angels.",
$"The world weeps for {Name}, whose presence will be sorely missed.",
$"In death, {Name} becomes a part of something greater than themselves.",
$"As {Name} fades away, their spirit is lifted by the love of those they leave behind.",
$"The last thing {Name} hears is the sound of loved ones saying goodbye.",
$"In their final moments, {Name} knows that their spirit will live on forever.",
$"As {Name} takes their final breath, they are at peace with the universe.",
$"{Name} passes away, leaving behind a legacy that will never be forgotten.",
$"In death, {Name} discovers the secrets of the universe and finds peace.",
$"As the light fades from {Name}'s eyes, they find comfort in the knowledge that they lived a good life.",
$"The world mourns the loss of {Name}, whose light shone brightly while they were alive.",
$"In their final moments, {Name} is surrounded by love and warmth.",
$"{Name} takes their final journey, leaving behind a world that will never be the same.",
$"As {Name} slips away, they are embraced by the love of those they leave behind.",
$"The world is a better place for having had {Name} in it, even if only for a little while.",
$"In death, {Name} is reunited with loved ones and friends who have gone before.",
$"As {Name} draws their last breath, they are at peace with the world.",
$"The last thing {Name} feels is the warmth of the sun on their face.", 
$"{Name} leaves this world, knowing that they have left a positive impact on those around them.",
$"{Name} departs with a sense of fulfillment, having lived a life true to themselves.",
$"In their final moments, {Name} is surrounded by the beauty of nature, finding peace in the natural world.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through their art.",
$"As {Name} takes their final breath, they find solace in the thought of being reunited with their beloved pets.",
$"{Name} passes away with a smile on their face, grateful for the love and support of their family and friends.",
$"In death, {Name} finds the answers to the mysteries that eluded them in life.",
$"{Name} slips away peacefully, leaving behind a world that is a little brighter for having known them.",
$"As the light fades from {Name}'s eyes, they are filled with wonder at the beauty of the universe.",
$"The world is a quieter place without {Name}, whose laughter and joy will be sorely missed.",
$"{Name} leaves behind a legacy of kindness and generosity, inspiring those who come after them.",
$"In their final moments, {Name} is embraced by the beauty of the stars, finding comfort in the infinite expanse of the cosmos.",
$"As {Name} takes their final journey, they leave behind a world that is forever changed by their presence.",
$"{Name} passes away, leaving behind memories that will be cherished by all who knew them.",
$"In death, {Name} finds the answers to the questions that plagued them in life.",
$"{Name} slips away, leaving behind a world that is richer for having known them.",
$"As the light fades from {Name}'s eyes, they are filled with a sense of wonder at the interconnectedness of all things.",
$"The world mourns the loss of {Name}, a bright and shining star that has gone out too soon.",
$"{Name} leaves this world with the knowledge that they have made a difference in the lives of others.",
$"In their final moments, {Name} is surrounded by the beauty of nature, finding peace in the cycles of life and death.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through their children.",
$"As {Name} takes their final breath, they find solace in the thought of being reunited with their soulmate.",
$"{Name} passes away with the knowledge that they have lived a life true to their values.",
$"In death, {Name} discovers the interconnectedness of all things, finding peace in the unity of the universe.",
$"{Name} slips away quietly, leaving behind a world that is forever changed by their legacy.",
$"As the light fades from {Name}'s eyes, they are filled with a sense of wonder at the beauty of creation.",
$"The world is a kinder place for having had {Name} in it, even if only for a little while.",
$"{Name} leaves behind a legacy of hope and inspiration, encouraging others to strive for greatness.",
$"In their final moments, {Name} is surrounded by the love and support of their community, finding solace in the bonds of human connection.",
$"{Name} closes their eyes for the last time, knowing that their spirit will live on through the memories of those they leave behind.",
$"{Name} gasps their last breath, the room filled with a haunting silence.",
$"{Name} stares into the abyss of death, their eyes filled with a cold, empty darkness.",
$"In their final moments, {Name} screams in agony, begging for mercy that will never come.",
$"{Name} falls to the floor, a lifeless shell devoid of all meaning and purpose.",
$"As {Name} takes their final breath, a chill fills the air, as if Death himself has arrived.",
$"{Name} dies alone, their final thoughts filled with regret and despair.",
$"In death, {Name} becomes a hollow, empty vessel, lost in the void of the afterlife.",
$"{Name} is consumed by the darkness of death, their soul doomed to eternal torment.",
$"As {Name} draws their final breath, a sense of dread fills the room, as if something evil is watching from the shadows.",
$"{Name} leaves this world, a trail of destruction and pain in their wake.",
$"In their final moments, {Name} is tormented by visions of a hellish landscape, their fate forever sealed.",
$"{Name} falls to the ground, their body wracked with pain, as they succumb to the icy grip of death.",
$"As {Name} takes their final journey, they are consumed by a darkness that will never fade.",
$"{Name} dies screaming, their voice echoing through the halls of the afterlife.",
$"In death, {Name} is swallowed by a void of eternal nothingness, their spirit lost forever.",
$"{Name} meets their end, a victim of the cruel and merciless hand of fate.",
$"{Name} breathes their last, a shadow of their former self, stripped of all vitality and strength.",
$"As {Name} slips away, a cold, foreboding presence fills the room, as Death claims another soul.",
$"{Name} departs this world, leaving behind a trail of darkness and despair that will never be forgotten.",
$"In their final moments, {Name} is consumed by a sense of futility, as if their life had no meaning or purpose.",
$"{Name} dies alone and forgotten, a mere footnote in the annals of time.",
$"As {Name} takes their final breath, a sense of dread fills the air, as if something malevolent is lurking just beyond the veil.",
$"{Name} is consumed by the fires of hell, their soul forever trapped in a prison of agony and despair.",
$"{Name} falls to the ground, their lifeblood staining the earth, a sacrifice to the dark powers that be.",
$"In death, {Name} is condemned to wander the afterlife, a lost and tormented spirit, forever searching for redemption.",
$"{Name} meets their end with a sense of resignation, knowing that their fate was sealed from the moment they were born.",
$"As {Name} draws their last breath, the very fabric of reality seems to warp and twist, as if the universe itself is mourning their passing.",
$"{Name} leaves this world, a curse upon their lips, a warning to all who dare to follow in their footsteps.",
$"In their final moments, {Name} is consumed by a darkness that blots out all hope and light.",
$"{Name} dies alone and forgotten, their passing unnoticed by a world that had long since moved on.",
$"As {Name} takes their final journey, they are consumed by a sense of regret, knowing that they will never see the light of day again.",
$"{Name} takes their final breath, their eyes filled with terror as they realize the true nature of death.",
$"{Name} dies screaming, their soul ripped apart by forces beyond their comprehension.",
$"In their final moments, {Name} is consumed by a sense of dread, as if something evil is lurking just beyond their field of vision.",
$"{Name} falls to the ground, their body twisted and contorted by the agony of their passing.",
$"As {Name} takes their final breath, the air around them grows thick with a noxious, choking mist.",
$"{Name} dies alone and afraid, their screams echoing through the empty halls of the afterlife.",
$"In death, {Name} is consumed by a darkness that blots out all hope and light, their soul forever trapped in a void of eternal suffering.",
$"{Name} meets their end, consumed by a madness that is beyond comprehension.",
$"As {Name} slips away, a cold, clammy hand reaches out to claim their soul, pulling them down into the abyss of death.",
$"{Name} falls to the ground, their eyes wide with terror, as they are dragged screaming into the jaws of oblivion.",
$"In their final moments, {Name} is consumed by a sense of overwhelming horror, as they realize that they are not alone in the darkness.",
$"{Name} dies alone, their body wracked with pain and terror, as they are dragged down into the pits of hell.",
$"As {Name} takes their final journey, they are pursued by a legion of howling demons, eager to claim their soul for their master.",
$"{Name} is consumed by the fires of hell, their screams echoing through the halls of the damned.",
$"In death, {Name} is lost in a labyrinth of terror and despair, forever tormented by the horrors that await them in the afterlife.",
$"{Name} falls to the ground, their body wracked with convulsions, as they are consumed by a darkness that will never fade.",
$"As {Name} draws their last breath, they are consumed by a sense of overwhelming dread, as they realize that they are about to face judgment for their sins.",
$"{Name} dies alone, their body twisted and contorted by the malevolent forces that claimed their soul.",
$"In their final moments, {Name} is consumed by a sense of overwhelming terror, as they realize that they are being pursued by a malevolent entity that seeks to claim their soul.",
$"{Name} takes their final breath, their eyes filled with terror, as they are dragged screaming into the abyss of death."
                        };

                    // Pick a random death message
                        string deathMessage = deathMessages[new Random().Next(0, deathMessages.Count)];
                        Room currentRoom = _server.GetRoomById(CurrentRoomId);

                        // Send the death message to all players in the room
                        foreach (Soul soul in currentRoom.PlayersInRoom)
                        {
                            soul.Connection.SendMessage($"\r\n{deathMessage}\r\n");
                        }            StartDecompositionTimer(_server);
            // Roll a d10 to see if the room becomes bloody
            Random random = new Random();
            int roll = random.Next(1, 11);

            if (roll == 1)
            {
                currentRoom.IsBloody = true;
                currentRoom.SendMessageToAllPlayers($"\x1b[91mBlood spills from {Name}!\x1b[0m\r\n");
                // Start a timer to clean up the room after 10 minutes
                System.Timers.Timer cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(10).TotalMilliseconds);
                cleanupTimer.AutoReset = false;
                cleanupTimer.Elapsed += (sender, e) =>
                {
                    currentRoom.IsBloody = false;
                    cleanupTimer.Dispose();
                    cleanupTimer.Start();

                };
            }

                Console.WriteLine($"{Name} has been killed. Distributing experience...");
                // Get the highest threat target
                Soul highestThreatTarget = GetHighestThreatTarget();

                // Check if the players are in a group
                bool playersAreGrouped = highestThreatTarget.Group != null && highestThreatTarget.Group.Members.Count > 1;

                // Distribute experience based on the group status
                if (playersAreGrouped)
                {
                    DistributeExperienceToGroup(highestThreatTarget.Group);
                }
                else
                {
                    DistributeExperienceToPlayer(highestThreatTarget);
                }

                // Clear the threat table
                ThreatLevels.Clear();
            }
        }
    }
