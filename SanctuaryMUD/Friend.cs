using System;

namespace SanctuaryMUD
{

    public class Friend
    {
        public Soul Player { get; set; }
        public bool IsAccepted { get; set; }

        public Friend(Soul player)
        {
            Player = player;
            IsAccepted = false;
        }

        public void Accept()
        {
            IsAccepted = true;
            Console.WriteLine($"You are now friends with {Player.Name}!");
        }

        public void Reject()
        {
            Console.WriteLine($"You have rejected {Player.Name}'s friend request.");
        }
    }
}