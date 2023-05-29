namespace SanctuaryMUD
{
    public class CraftingManager
    {
        private List<CraftingRecipe> _recipes;
        private Action<string, int, double> _increaseSkill;


        public CraftingManager(Action<string, int, double> increaseSkill)
        {
            _recipes = new List<CraftingRecipe>
            {
                // Add your crafting recipes here.
                new CraftingRecipe(new List<string> { "dirty flesh" }, "leather", "Tailoring"),
                new CraftingRecipe(new List<string> { "leather", "leather" }, "leather gloves", "Tailoring"),
                // ... more recipes
            };
            _increaseSkill = increaseSkill;

        }

        public bool CombineItems(Soul player, List<string> itemsToCombine, out Item outputItem,
            out string resultMessage)
        {
            Console.WriteLine("Entering CombineItems method.");
            outputItem = null;
            resultMessage = "";

            var matchingRecipe = _recipes.FirstOrDefault(recipe => recipe.MatchesRecipe(itemsToCombine));

            if (matchingRecipe != null)
            {
                Console.WriteLine("Found matching recipe.");

                if (HasItems(player.Inventory, itemsToCombine))
                {
                    Console.WriteLine("Player has required items. Attempting to skill up.");
                    Console.WriteLine("Removing input items from player's inventory.");

                    RemoveItems(player.Inventory, itemsToCombine);
                    Console.WriteLine("Input items removed.");
                    Console.WriteLine("Creating output item.");

                    outputItem = CreateItemFromString(matchingRecipe.OutputItem);

                    Console.WriteLine("Adding output item to player's inventory.");
                    bool inventorySlotFound = false;
                    int maxInventorySlots = 20; // Replace this with the actual maximum number of inventory slots

                    for (int i = 1; i <= maxInventorySlots; i++)
                    {
                        if (!player.Inventory.ContainsKey(i))
                        {
                            player.Inventory[i] = outputItem;
                            inventorySlotFound = true;
                            resultMessage =
                                $"You successfully combined the items and created {outputItem.Name}. It has been placed in your inventory.\r\n";
                            break;
                        }
                    }

                    if (!inventorySlotFound)
                    {
                        resultMessage = "Your inventory is full. Please free up some space and try again.\r\n";
                    }

                    Console.WriteLine("Output item added to player's inventory.");

                    int currentSkillValue = player.Skills[matchingRecipe.CraftingType].CurrentValue;
                    double baseChance = 0.05;

                    // Execute skill-up formula directly within the CombineItems method
                    double skillIncreaseChance = baseChance + (1.0 - currentSkillValue / 100.0) * 0.95;
                    if (new Random().NextDouble() <= skillIncreaseChance)
                    {
                        player.Skills[matchingRecipe.CraftingType].CurrentValue += 1;
                    }

                    Console.WriteLine("Skill up logic executed.");

                    return true;
                }
            }

            Console.WriteLine("Exiting CombineItems method.");
            return false;
        }




        private Item CreateItemFromString(string itemName)
        {
            // Retrieve the item from the database using the GetItem method and the item's name
            Item item = ItemManager.GetItemByName(itemName.ToLower());

            if (item != null)
            {
                return item;
            }

            return null; // Return null if the item name is not recognized
        }


        private bool HasItems(Dictionary<int, Item> inventory, List<string> requiredItems)
        {
            Console.WriteLine("Checking if player has required items."); // Debug message

            var itemCounts = new Dictionary<string, int>();

            foreach (var item in requiredItems)
            {
                if (itemCounts.ContainsKey(item))
                {
                    itemCounts[item]++;
                }
                else
                {
                    itemCounts[item] = 1;
                }
            }

            Console.WriteLine("Required item counts:"); // Debug message
            foreach (var kvp in itemCounts)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}"); // Debug message
            }

            Console.WriteLine("Player inventory:"); // Debug message
            foreach (var kvp in inventory)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value.Name}"); // Debug message
            }

            foreach (var kvp in inventory)
            {
                string itemNameLower = kvp.Value.Name.ToLower(); // Convert the name to lowercase
                if (itemCounts.ContainsKey(itemNameLower))
                {
                    itemCounts[itemNameLower]--;
                    if (itemCounts[itemNameLower] == 0)
                    {
                        itemCounts.Remove(itemNameLower);
                    }
                }

                if (itemCounts.Count == 0)
                {
                    Console.WriteLine("Player has required items."); // Debug message
                    return true;
                }
            }

            Console.WriteLine("Player does not have required items."); // Debug message
            Console.WriteLine("Remaining item counts:"); // Debug message
            foreach (var kvp in itemCounts)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}"); // Debug message
            }

            return false;
        }



        private void RemoveItems(Dictionary<int, Item> inventory, List<string> itemsToRemove)
        {
            Console.WriteLine("Removing input items from player's inventory."); // Debug message

            foreach (var itemName in itemsToRemove)
            {
                var itemKey = inventory
                    .FirstOrDefault(kvp => kvp.Value.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)).Key;
                inventory.Remove(itemKey);
            }

            Console.WriteLine("Input items removed."); // Debug message
        }
    }
}