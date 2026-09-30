using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record AddMemberCommand(Guid ChatId, string UserIdToAdd, string RequesterId) : IRequest<MediatR.Unit>;
}
