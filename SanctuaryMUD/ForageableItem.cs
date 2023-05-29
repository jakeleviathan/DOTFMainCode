namespace SanctuaryMUD
{
    public class ForageableItem : Item
    {
        public int SuccessChance { get; set; }

        public ForageableItem(int id, int successChance)
        {
            // Retrieve the item from the database using the GetItem method and the item's ID
            Item item = ItemManager.GetItem(id);

            if (item != null)
            {
                // Initialize the base class with the properties of the retrieved item
                base.ID = item.ID;
                base.Name = item.Name;
                base.Slot = item.Slot;
                base.SlotNumber = item.SlotNumber;
                base.Str = item.Str;
                base.Dex = item.Dex;
                base.Wis = item.Wis;
                base.Int = item.Int;
                base.Con = item.Con;
                base.Cha = item.Cha;
                base.Type = item.Type;
                base.Alias = item.Alias;
                base.IsFashion = item.IsFashion;
                base.Description = item.Description;

                SuccessChance = successChance;
            }
            else
            {
                throw new ArgumentException("Item with the specified ID not found in the database.");
            }
        }

    }
}