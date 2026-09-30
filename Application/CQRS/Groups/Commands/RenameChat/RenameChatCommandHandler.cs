using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class RenameChatCommandHandler : IRequestHandler<RenameChatCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public RenameChatCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(RenameChatCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");
            if (!chat.IsGroup) throw new InvalidOperationException("Only groups can be renamed.");

            // Any member, not just the owner — same invite-model reasoning as
            // AddMemberCommandHandler.
            var requesterIsMember = await _chats.IsMemberAsync(request.ChatId, request.RequesterId);
            if (!requesterIsMember)
                throw new UnauthorizedAccessException("Only chat members can rename the chat");

            chat.Rename(request.Name);
            await _chats.UpdateAsync(chat);

            // Per-member, not NotifyGroupAsync(Clients.Group) — that only
            // reaches members who currently have this chat open (see
            // OutboxDispatcher's NewMessage fix / DeleteChatCommandHandler's
            // comment for the same reasoning).
            foreach (var member in chat.Members)
            {
                try
                {
                    await _notifier.NotifyUserAsync(member.UserId, "ChatUpdated", new
                    {
                        ChatId = request.ChatId,
                        Name = chat.Name,
                        AvatarUrl = chat.AvatarUrl
                    });
                }
                catch { }
            }

            return MediatR.Unit.Value;
        }
    }
}
