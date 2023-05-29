namespace SanctuaryMUD
{
    public abstract class Entity
    {
        public string Name { get; set; }
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; }
        public int MaxMovementPoints { get; set; }
        public int MovementPoints { get; set; }
        public int CurrentMana { get; set; }
        public int MaxMana { get; set; }
        public int HealthRegenModifier { get; set; }
        public int ManaRegenModifier { get; set; }
        public bool IsAlive { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsPetrified { get; set; }

        // Add other common properties and methods
        public Entity()
        {
            IsAttacking = false;
            // Add any other default property values here
        }
    }
}
