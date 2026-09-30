using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Chats.DTOs;

namespace Application.CQRS.Chats.Queries
{
    public class GetMyChatsQueryHandler : IRequestHandler<GetMyChatsQuery, IReadOnlyList<ChatDto>>
    {
        private readonly IChatRepository _chats;
        private readonly IUserRepository _users;

        public GetMyChatsQueryHandler(IChatRepository chats, IUserRepository users)
        {
            _chats = chats;
            _users = users;
        }

        public async Task<IReadOnlyList<ChatDto>> Handle(GetMyChatsQuery request, CancellationToken cancellationToken)
        {
            var chats = (await _chats.GetAllChatsForUserAsync(request.UserId)).ToList();

            // Личные чаты с забаненным собеседником скрываем целиком, из групп
            // убираем только самого забаненного участника.
            var banned = await _users.GetBannedIdsAsync(chats.SelectMany(c => c.Members.Select(m => m.UserId)));
            if (banned.Count > 0)
            {
                chats = chats
                    .Where(c => c.IsGroup || !c.Members.Any(m => m.UserId != request.UserId && banned.Contains(m.UserId)))
                    .ToList();
            }

            var chatIds = chats.Select(c => c.Id);
            var unreadCounts = await _chats.GetUnreadCountsAsync(chatIds, request.UserId);

            return chats
                .Select(c => ChatDto.FromEntity(c, unreadCounts.TryGetValue(c.Id, out var count) ? count : 0, banned))
                .ToList();
        }
    }
}
