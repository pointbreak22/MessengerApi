using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record RenameChatCommand(Guid ChatId, string RequesterId, string Name) : IRequest<MediatR.Unit>;
}
