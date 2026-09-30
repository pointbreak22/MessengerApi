using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Chats.Queries
{
    public class IsChatMemberQueryHandler : IRequestHandler<IsChatMemberQuery, bool>
    {
        private readonly IChatRepository _chats;

        public IsChatMemberQueryHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public Task<bool> Handle(IsChatMemberQuery request, CancellationToken cancellationToken) =>
            _chats.IsMemberAsync(request.ChatId, request.UserId);
    }
}
