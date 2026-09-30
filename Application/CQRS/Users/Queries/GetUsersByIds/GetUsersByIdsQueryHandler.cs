using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public class GetUsersByIdsQueryHandler : IRequestHandler<GetUsersByIdsQuery, IReadOnlyList<UserDto>>
    {
        private readonly IUserRepository _users;

        public GetUsersByIdsQueryHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<IReadOnlyList<UserDto>> Handle(GetUsersByIdsQuery request, CancellationToken cancellationToken)
        {
            var users = await _users.GetByIdsAsync(request.Ids);
            return users.Select(UserDto.FromEntity).ToList();
        }
    }
}
