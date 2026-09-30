using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Chats.Queries
{
    // Звонок разрешён только внутри существующего личного чата между звонящим и адресатом —
    // это не даёт позвонить произвольному userId в обход списка собеседников.
    public class CanInitiateCallQueryHandler : IRequestHandler<CanInitiateCallQuery, bool>
    {
        private readonly IChatRepository _chats;

        public CanInitiateCallQueryHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public async Task<bool> Handle(CanInitiateCallQuery request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null || chat.IsGroup) return false;

            var memberIds = chat.Members.Select(m => m.UserId).ToHashSet();
            return memberIds.Contains(request.CallerId) && memberIds.Contains(request.TargetUserId);
        }
    }
}
