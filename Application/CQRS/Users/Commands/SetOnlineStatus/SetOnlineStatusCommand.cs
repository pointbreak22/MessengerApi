using MediatR;

namespace Application.CQRS.Users.Commands
{
    public record SetOnlineStatusCommand(string UserId, bool IsOnline) : IRequest<Unit>;
}
