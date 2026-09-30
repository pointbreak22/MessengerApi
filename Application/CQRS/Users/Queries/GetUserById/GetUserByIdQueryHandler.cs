using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
    {
        private readonly IUserRepository _users;

        public GetUserByIdQueryHandler(IUserRepository users)
        {
            _users = users;
        }

        public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _users.GetByIdAsync(request.Id);
            return user == null || user.IsBanned ? null : UserDto.FromEntity(user);
        }
    }
}
