using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record RemoveMemberCommand(Guid ChatId, string UserIdToRemove, string RequesterId) : IRequest<MediatR.Unit>;
}
