using MediatR;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public record GetUserByIdQuery(string Id) : IRequest<UserDto?>;
}
