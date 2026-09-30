using MediatR;

namespace Application.CQRS.Friends.Commands
{
    public record RemoveFriendCommand(string RequesterId, string FriendId) : IRequest<MediatR.Unit>;
}
