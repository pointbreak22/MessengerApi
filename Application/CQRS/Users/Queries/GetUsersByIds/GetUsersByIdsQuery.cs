using System.Collections.Generic;
using MediatR;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public record GetUsersByIdsQuery(IReadOnlyList<string> Ids) : IRequest<IReadOnlyList<UserDto>>;
}
