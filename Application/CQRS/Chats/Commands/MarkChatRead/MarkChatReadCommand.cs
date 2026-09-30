using System;
using MediatR;

namespace Application.CQRS.Chats.Commands
{
    public record MarkChatReadCommand(Guid ChatId, string UserId) : IRequest<Unit>;
}
