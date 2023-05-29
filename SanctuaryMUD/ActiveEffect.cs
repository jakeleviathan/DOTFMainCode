namespace SanctuaryMUD;
public class ActiveEffect
{
    public string Name { get; set; }
    public DateTime ExpirationTime { get; set; }

    public ActiveEffect(string name, DateTime expirationTime)
    {
        Name = name;
        ExpirationTime = expirationTime;
    }
}
