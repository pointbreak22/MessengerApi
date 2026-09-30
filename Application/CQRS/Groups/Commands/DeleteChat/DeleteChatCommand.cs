using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record DeleteChatCommand(Guid ChatId, string RequesterId) : IRequest<MediatR.Unit>;
}
