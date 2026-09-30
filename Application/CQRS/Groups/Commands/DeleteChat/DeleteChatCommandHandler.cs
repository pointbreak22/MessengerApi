using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class DeleteChatCommandHandler : IRequestHandler<DeleteChatCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public DeleteChatCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(DeleteChatCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");

            if (chat.OwnerId != request.RequesterId)
                throw new UnauthorizedAccessException("Only the owner can delete the chat");

            var groupId = request.ChatId.ToString();

            // NotifyGroupAsync only reaches sockets that called JoinChatRoom, i.e.
            // members who currently have this chat open — most members don't.
            // Notify (and unsubscribe) every member individually instead, so the
            // event reaches everyone regardless of what they have open right now.
            foreach (var member in chat.Members)
            {
                try { await _notifier.NotifyUserAsync(member.UserId, "ChatDeleted", new { ChatId = request.ChatId }); } catch { }
                try { await _notifier.RemoveUserFromGroupAsync(member.UserId, groupId); } catch { }
            }

            await _chats.DeleteAsync(chat);

            return MediatR.Unit.Value;
        }
    }
}
