using System;

namespace Application.CQRS.Friends.DTOs
{
    public record FriendRequestDto(Guid Id, string FromUserId, DateTime UpdatedAt);
}
