using System;
using MediatR;

namespace Application.CQRS.Messages.Commands
{
    public record SendMessageCommand(
        Guid ChatId,
        string SenderId,
        string Text,
        string? IdempotencyKey = null,
        string? AttachmentUrl = null,
        Guid? ReplyToMessageId = null) : IRequest<Guid>;
}
