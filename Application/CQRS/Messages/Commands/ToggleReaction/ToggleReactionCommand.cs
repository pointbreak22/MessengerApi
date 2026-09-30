using System;
using System.Collections.Generic;
using MediatR;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Commands
{
    public record ToggleReactionCommand(Guid MessageId, string RequesterId, string Emoji)
        : IRequest<IReadOnlyList<MessageReactionDto>>;
}
