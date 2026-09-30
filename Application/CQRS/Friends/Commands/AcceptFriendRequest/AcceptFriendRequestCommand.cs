using MediatR;

namespace Application.CQRS.Friends.Commands
{
    public record AcceptFriendRequestCommand(string RequesterId, string AccepterId) : IRequest<MediatR.Unit>;
}
