using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public class GetFriendsQueryHandler : IRequestHandler<GetFriendsQuery, IReadOnlyList<UserDto>>
    {
        private readonly IUserRepository _users;

        public GetFriendsQueryHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<IReadOnlyList<UserDto>> Handle(GetFriendsQuery request, CancellationToken cancellationToken)
        {
            var friends = await _users.GetFriendsAsync(request.UserId);
            return friends.Select(UserDto.FromEntity).ToList();
        }
    }
}
