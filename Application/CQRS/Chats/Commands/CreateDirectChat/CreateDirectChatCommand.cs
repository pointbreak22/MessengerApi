using System;
using MediatR;

namespace Application.CQRS.Chats.Commands
{
    public record CreateDirectChatCommand(string UserId, string TargetUserId) : IRequest<Guid>;
}
