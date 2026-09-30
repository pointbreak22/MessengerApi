using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Entities;

namespace Application.CQRS.Chats.DTOs
{
    public record ChatDto(Guid Id, string? Name, string? AvatarUrl, bool IsGroup, bool IsPublic, string? OwnerId, DateTime CreatedAt, IReadOnlyList<ChatMemberDto> Members, int UnreadCount = 0)
    {
        // hiddenUserIds — забаненные участники: в списке участников их не показываем.
        public static ChatDto FromEntity(Chat chat, int unreadCount = 0, ISet<string>? hiddenUserIds = null) =>
            new(chat.Id, chat.Name, chat.AvatarUrl, chat.IsGroup, chat.IsPublic, chat.OwnerId, chat.CreatedAt,
                chat.Members
                    .Where(m => hiddenUserIds == null || !hiddenUserIds.Contains(m.UserId))
                    .Select(ChatMemberDto.FromEntity)
                    .ToList(),
                unreadCount);
    }
}
