using MediatR;
using Application.CQRS.Friends.DTOs;

namespace Application.CQRS.Friends.Queries
{
    public record FindFriendshipQuery(string UserId, string FriendId) : IRequest<FriendshipDto?>;
}
