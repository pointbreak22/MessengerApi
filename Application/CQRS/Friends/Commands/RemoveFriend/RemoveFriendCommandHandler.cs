using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Friends.Commands
{
    public class RemoveFriendCommandHandler : IRequestHandler<RemoveFriendCommand, MediatR.Unit>
    {
        private readonly IFriendshipRepository _friendship;

        public RemoveFriendCommandHandler(IFriendshipRepository friendship)
        {
            _friendship = friendship;
        }

        public async Task<MediatR.Unit> Handle(RemoveFriendCommand request, CancellationToken cancellationToken)
        {
            var existing = await _friendship.FindRequestAsync(request.RequesterId, request.FriendId);
            if (existing == null) return Unit.Value;

            await _friendship.RemoveFriendAsync(existing.Id);
            return MediatR.Unit.Value;
        }
    }
}
