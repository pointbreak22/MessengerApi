using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public class GetChatByIdQueryHandler : IRequestHandler<GetChatByIdQuery, ChatDto?>
    {
        private readonly IChatRepository _chats;
        private readonly IUserRepository _users;

        public GetChatByIdQueryHandler(IChatRepository chats, IUserRepository users)
        {
            _chats = chats;
            _users = users;
        }

        public async Task<ChatDto?> Handle(GetChatByIdQuery request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) return null;

            var banned = await _users.GetBannedIdsAsync(chat.Members.Select(m => m.UserId));
            return ChatDto.FromEntity(chat, hiddenUserIds: banned);
        }
    }
}
