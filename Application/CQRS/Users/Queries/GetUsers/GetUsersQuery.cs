using System.Collections.Generic;
using MediatR;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Queries
{
    public record GetUsersQuery(int Page, int PageSize, string? Search, string? SortBy, bool? IsOnline)
        : IRequest<(IReadOnlyList<UserDto> Items, int Total)>;
}
