using System;
using Domain.Entities;

namespace Application.CQRS.Chats.DTOs
{
    public record ChatMemberDto(string UserId, DateTime JoinedAt, DateTime? LastReadAt)
    {
        public static ChatMemberDto FromEntity(ChatMember member) =>
            new(member.UserId, member.JoinedAt, member.LastReadAt);
    }
}
