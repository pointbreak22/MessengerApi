using System;
using System.Collections.Generic;
using Domain.Entities;

namespace Application.CQRS.Messages.DTOs
{
    public record MessageReactionDto(string Emoji, string UserId);

    public record MessageDto(
        Guid Id,
        Guid ChatId,
        string SenderId,
        string Text,
        string? AttachmentUrl,
        DateTime CreatedAt,
        DateTime? EditedAt,
        IReadOnlyList<MessageReactionDto> Reactions,
        Guid? ReplyToMessageId)
    {
        public static MessageDto FromEntity(Message message, IReadOnlyList<MessageReactionDto>? reactions = null) =>
            new(message.Id, message.ChatId, message.SenderId, message.Text, message.AttachmentUrl,
                message.CreatedAt, message.EditedAt, reactions ?? Array.Empty<MessageReactionDto>(), message.ReplyToMessageId);
    }
}
