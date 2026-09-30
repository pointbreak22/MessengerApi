using System;
using MediatR;

namespace Application.CQRS.Groups.Commands
{
    // AvatarUrl null clears it (e.g. remove-avatar).
    public record UpdateChatAvatarCommand(Guid ChatId, string RequesterId, string? AvatarUrl) : IRequest<MediatR.Unit>;
}
