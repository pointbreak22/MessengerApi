using System.Collections.Generic;
using MediatR;
using Application.CQRS.Friends.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public record GetPendingFriendRequestsQuery(string UserId) : IRequest<IReadOnlyList<FriendRequestDto>>;
}
