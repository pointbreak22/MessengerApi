using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Friends.Commands
{
    public class SendFriendRequestCommandHandler : IRequestHandler<SendFriendRequestCommand, MediatR.Unit>
    {
        private readonly IFriendshipRepository _friendship;
        private readonly IRealtimeNotifier _notifier;

        public SendFriendRequestCommandHandler(IFriendshipRepository friendship, IRealtimeNotifier notifier)
        {
            _friendship = friendship;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(SendFriendRequestCommand request, CancellationToken cancellationToken)
        {
            var f = await _friendship.CreateRequestAsync(request.RequesterId, request.FriendId);
            try { await _notifier.NotifyUserAsync(request.FriendId, "FriendRequest", new { From = request.RequesterId, FriendshipId = f.Id }); } catch { }
            return MediatR.Unit.Value;
        }
    }
}
