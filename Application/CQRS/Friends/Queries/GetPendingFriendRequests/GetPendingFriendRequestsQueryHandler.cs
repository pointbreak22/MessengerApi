using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Domain.Enums;
using Application.CQRS.Friends.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public class GetPendingFriendRequestsQueryHandler : IRequestHandler<GetPendingFriendRequestsQuery, IReadOnlyList<FriendRequestDto>>
    {
        private readonly IFriendshipRepository _friendship;
        private readonly IUserRepository _users;

        public GetPendingFriendRequestsQueryHandler(IFriendshipRepository friendship, IUserRepository users)
        {
            _friendship = friendship;
            _users = users;
        }

        public async Task<IReadOnlyList<FriendRequestDto>> Handle(GetPendingFriendRequestsQuery request, CancellationToken cancellationToken)
        {
            var all = await _friendship.GetFriendshipsForUserAsync(request.UserId);
            var pending = all
                .Where(f => f.FriendId == request.UserId && f.Status == FriendshipStatus.Pending)
                .ToList();

            var banned = await _users.GetBannedIdsAsync(pending.Select(f => f.UserId));
            return pending
                .Where(f => !banned.Contains(f.UserId))
                .Select(f => new FriendRequestDto(f.Id, f.UserId, f.UpdatedAt))
                .ToList();
        }
    }
}
