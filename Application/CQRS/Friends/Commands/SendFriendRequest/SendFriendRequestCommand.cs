using MediatR;

namespace Application.CQRS.Friends.Commands
{
    public record SendFriendRequestCommand(string RequesterId, string FriendId) : IRequest<MediatR.Unit>;
}
