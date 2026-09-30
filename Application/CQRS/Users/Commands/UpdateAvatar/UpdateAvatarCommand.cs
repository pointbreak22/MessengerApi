using MediatR;

namespace Application.CQRS.Users.Commands
{
    public record UpdateAvatarCommand(string UserId, string? AvatarUrl) : IRequest<Unit>;
}
