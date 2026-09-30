using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Users.Commands
{
    public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Unit>
    {
        private readonly IUserRepository _users;

        public UpdateProfileCommandHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<Unit> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(request.UserId);
            if (user == null) return Unit.Value;

            user.UpdateProfile(request.UserName);
            await _users.UpdateAsync(user);
            return Unit.Value;
        }
    }
}
