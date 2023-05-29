namespace SanctuaryMUD;

public class AvailableSpell
{
    public int RequiredLevel { get; set; }
    public string RequiredSkill { get; set; }
    public int RequiredSkillValue { get; set; }
    public int AstralCost { get; set; }

    public AvailableSpell(int requiredLevel, string requiredSkill, int requiredSkillValue, int astralCost)
    {
        RequiredLevel = requiredLevel;
        RequiredSkill = requiredSkill;
        RequiredSkillValue = requiredSkillValue;
        AstralCost = astralCost;
    }
}
