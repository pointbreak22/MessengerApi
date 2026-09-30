using System.Collections.Generic;
using MediatR;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public record GetFriendsQuery(string UserId) : IRequest<IReadOnlyList<UserDto>>;
}
