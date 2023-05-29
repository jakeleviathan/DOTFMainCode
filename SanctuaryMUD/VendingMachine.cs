namespace SanctuaryMUD.Classes;

class VendingMachine
{
    private bool _isHacked;
    private Random _random;

    public VendingMachine()
    {
        _random = new Random();
        _isHacked = false;
    }

    public void Hack(Soul soul)
    {
        // Simulate the hacking process
        // You can implement a more complex system here based on the player's tools and decryption method
        _isHacked = _random.Next(0, 2) == 0; // 50% chance to hack
    }

    public void RetrieveReward(Soul soul)
    {
        if (!_isHacked)
        {
            Console.WriteLine("Hacking failed. Try again tomorrow.");
            return;
        }

        // Generate a random reward
        string[] rewards = new[] { "Nano-Booster", "Medical Kit", "Augmentation Module", "Stealth Cloak", "Prototype Gadget" };
        string reward = rewards[_random.Next(rewards.Length)];

        Console.WriteLine($"Congratulations, {soul.Name}! You've successfully hacked the vending machine and received a {reward}.");
    }
}