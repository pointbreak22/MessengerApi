using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Users.Commands
{
    public class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand, Unit>
    {
        private readonly IUserRepository _users;

        public UpdateAvatarCommandHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<Unit> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(request.UserId);
            if (user == null) return Unit.Value;

            user.UpdateAvatar(request.AvatarUrl);
            await _users.UpdateAsync(user);
            return Unit.Value;
        }
    }
}
