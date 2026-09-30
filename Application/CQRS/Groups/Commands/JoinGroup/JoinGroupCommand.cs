using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record JoinGroupCommand(Guid ChatId, string UserId) : IRequest<MediatR.Unit>;
}
