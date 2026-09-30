using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Users.Commands
{
    public class SetOnlineStatusCommandHandler : IRequestHandler<SetOnlineStatusCommand, Unit>
    {
        private readonly IUserRepository _userRepository;

        public SetOnlineStatusCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Unit> Handle(SetOnlineStatusCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user != null)
            {
                user.UpdateStatus(request.IsOnline);
                await _userRepository.UpdateAsync(user);
            }
            return Unit.Value;
        }
    }
}
