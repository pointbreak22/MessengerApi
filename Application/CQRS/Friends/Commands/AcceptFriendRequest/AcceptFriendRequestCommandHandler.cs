using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Friends.Commands
{
    public class AcceptFriendRequestCommandHandler : IRequestHandler<AcceptFriendRequestCommand, MediatR.Unit>
    {
        private readonly IFriendshipRepository _friendship;
        private readonly IRealtimeNotifier _notifier;

        public AcceptFriendRequestCommandHandler(IFriendshipRepository friendship, IRealtimeNotifier notifier)
        {
            _friendship = friendship;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(AcceptFriendRequestCommand request, CancellationToken cancellationToken)
        {
            var existing = await _friendship.FindRequestAsync(request.RequesterId, request.AccepterId);
            if (existing == null) return Unit.Value;

            await _friendship.AcceptRequestAsync(existing.Id);
            try { await _notifier.NotifyUserAsync(request.RequesterId, "FriendRequestAccepted", new { By = request.AccepterId }); } catch { }
            return MediatR.Unit.Value;
        }
    }
}
