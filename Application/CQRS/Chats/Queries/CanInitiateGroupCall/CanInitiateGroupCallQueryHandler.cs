using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Chats.Queries
{
    public class CanInitiateGroupCallQueryHandler : IRequestHandler<CanInitiateGroupCallQuery, bool>
    {
        private readonly IChatRepository _chats;

        public CanInitiateGroupCallQueryHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public async Task<bool> Handle(CanInitiateGroupCallQuery request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null || !chat.IsGroup) return false;

            var memberIds = chat.Members.Select(m => m.UserId).ToHashSet();
            if (!memberIds.Contains(request.CallerId)) return false;

            return request.ParticipantIds.All(memberIds.Contains);
        }
    }
}
