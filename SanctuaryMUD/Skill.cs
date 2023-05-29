namespace SanctuaryMUD;

public class Skill
{
    public string Name { get; set; }
    public int BaseValue { get; set; }
    public int CurrentValue { get; set; }

    public Skill(string name, int baseValue)
    {
        Name = name;
        BaseValue = baseValue;
        CurrentValue = baseValue;
    }
}
