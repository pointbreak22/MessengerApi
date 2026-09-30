using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public RemoveMemberCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");

            if (chat.OwnerId != request.RequesterId)
                throw new UnauthorizedAccessException("Only the owner can remove members");

            var isMember = await _chats.IsMemberAsync(request.ChatId, request.UserIdToRemove);
            if (!isMember) return MediatR.Unit.Value; // idempotent

            await _chats.RemoveMemberAsync(request.ChatId, request.UserIdToRemove);

            var groupId = request.ChatId.ToString();
            // Revoke the SignalR room subscription first — otherwise the removed
            // user keeps receiving NewMessage for this chat until they happen to
            // call LeaveChatRoom themselves, which a client can't be trusted to do.
            try { await _notifier.RemoveUserFromGroupAsync(request.UserIdToRemove, groupId); } catch { }
            try { await _notifier.NotifyGroupAsync(groupId, "GroupMemberRemoved", new { UserId = request.UserIdToRemove }); } catch { }
            try { await _notifier.NotifyUserAsync(request.UserIdToRemove, "RemovedFromGroup", new { ChatId = request.ChatId }); } catch { }

            return MediatR.Unit.Value;
        }
    }
}
