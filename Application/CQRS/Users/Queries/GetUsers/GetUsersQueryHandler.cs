using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, (IReadOnlyList<UserDto> Items, int Total)>
    {
        private readonly IUserRepository _users;

        public GetUsersQueryHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<(IReadOnlyList<UserDto> Items, int Total)> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            var (items, total) = await _users.GetPagedAsync(request.Page, request.PageSize, request.Search, request.SortBy, request.IsOnline);
            return (items.Select(UserDto.FromEntity).ToList(), total);
        }
    }
}
