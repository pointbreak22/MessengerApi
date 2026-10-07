using System;
using System.Collections.Generic;
using MediatR;
using Application.Moderation;
using Domain.Enums;

namespace Application.CQRS.Messages.Commands
{
    public record SendMessageCommand(
        Guid ChatId,
        string SenderId,
        string Text,
        string? IdempotencyKey = null,
        string? AttachmentUrl = null,
        Guid? ReplyToMessageId = null) : IRequest<Guid>, IModeratedRequest
    {
        string IModeratedRequest.ModerationActorId => SenderId;
        IEnumerable<ModeratedText> IModeratedRequest.GetModeratedTexts() => new[] { new ModeratedText(ModerationTarget.Message, Text, "отправка сообщения") };
    }
}
