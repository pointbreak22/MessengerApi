using MediatR;

namespace Application.CQRS.Users.Commands
{
    public record UpdateProfileCommand(string UserId, string UserName) : IRequest<Unit>;
}
