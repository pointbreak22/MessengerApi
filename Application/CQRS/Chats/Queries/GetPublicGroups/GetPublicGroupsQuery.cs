using System.Collections.Generic;
using MediatR;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public record GetPublicGroupsQuery(int Page, int PageSize, string? Search, string UserId)
        : IRequest<(IReadOnlyList<PublicGroupDto> Items, int Total)>;
}
