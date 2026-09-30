using System;
using System.Linq;
using Domain.Entities;

namespace Application.CQRS.Chats.DTOs
{
    public record PublicGroupDto(Guid Id, string Name, string? AvatarUrl, int MemberCount, bool IsMember)
    {
        public static PublicGroupDto FromEntity(Chat chat, string userId) =>
            new(chat.Id, chat.Name ?? string.Empty, chat.AvatarUrl, chat.Members.Count, chat.Members.Any(m => m.UserId == userId));
    }
}
