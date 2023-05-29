namespace SanctuaryMUD;

public class Group
{
    public List<Soul> Members { get; set; }
    public Soul Leader { get; set; }

    public Group()
    {
        Members = new List<Soul>();
    }
}
