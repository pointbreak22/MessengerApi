using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    public record CreateGroupCommand(string Name, string OwnerId, string[]? MemberIds, bool IsPublic = false) : IRequest<Guid>;
}
