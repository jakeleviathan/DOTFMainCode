// File: FriendList.cs

using System.Collections.Generic;

namespace SanctuaryMUD
{
    public class FriendList
    {
        private List<string> friends;

        public FriendList()
        {
            friends = new List<string>();
        }

        public void AddFriend(string player)
        {
            if (!friends.Contains(player))
            {
                friends.Add(player);
            }
        }

        public void RemoveFriend(string player)
        {
            if (friends.Contains(player))
            {
                friends.Remove(player);
            }
        }

        public bool IsFriend(string player)
        {
            return friends.Contains(player);
        }
    }
}