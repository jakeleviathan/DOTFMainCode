namespace SanctuaryMUD;

public class LootItem
{
    public Item Item { get; set; }
    public int DropChance { get; set; }

    public LootItem(Item item, int dropChance)
    {
        Item = item;
        DropChance = dropChance;
    }
}
