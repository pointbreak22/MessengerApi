using System;
using Domain.Entities;
using Domain.Enums;

namespace Application.CQRS.Friends.DTOs
{
    public record FriendshipDto(Guid Id, string UserId, string FriendId, FriendshipStatus Status, DateTime UpdatedAt)
    {
        public static FriendshipDto FromEntity(Friendship friendship) =>
            new(friendship.Id, friendship.UserId, friendship.FriendId, friendship.Status, friendship.UpdatedAt);
    }
}
