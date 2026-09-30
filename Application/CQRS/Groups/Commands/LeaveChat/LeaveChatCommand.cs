using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record LeaveChatCommand(Guid ChatId, string UserId) : IRequest<MediatR.Unit>;
}
