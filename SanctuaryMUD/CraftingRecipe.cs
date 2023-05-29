namespace SanctuaryMUD;

public class CraftingRecipe
{
    public List<string> InputItems { get; set; }
    public string OutputItem { get; set; }
    public string CraftingType { get; set; }


    public CraftingRecipe(List<string> inputItems, string outputItem, string craftingType)
    {
        InputItems = inputItems;
        OutputItem = outputItem;
        CraftingType = craftingType;
    }

    public bool MatchesRecipe(List<string> itemsToCombine)
    {
        return InputItems.OrderBy(i => i).SequenceEqual(itemsToCombine.OrderBy(i => i));
    }
}
