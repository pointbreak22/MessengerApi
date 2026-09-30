using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Friends.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public class FindFriendshipQueryHandler : IRequestHandler<FindFriendshipQuery, FriendshipDto?>
    {
        private readonly IFriendshipRepository _friendship;

        public FindFriendshipQueryHandler(IFriendshipRepository friendship)
        {
            _friendship = friendship;
        }

        public async Task<FriendshipDto?> Handle(FindFriendshipQuery request, CancellationToken cancellationToken)
        {
            var found = await _friendship.FindRequestAsync(request.UserId, request.FriendId);
            return found == null ? null : FriendshipDto.FromEntity(found);
        }
    }
}
