using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Friendship
    {
        public Guid Id { get; private set; }
        public string UserId { get; private set; }       // Кто отправил запрос
        public string FriendId { get; private set; }     // Кому отправили
        public FriendshipStatus Status { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private Friendship() { }

        public static Friendship CreateRequest(string userId, string friendId)
        {
            if (userId == friendId) throw new ArgumentException("Нельзя добавиться в друзья к самому себе.");
            return new Friendship
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FriendId = friendId,
                Status = FriendshipStatus.Pending,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Accept()
        {
            Status = FriendshipStatus.Accepted;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
