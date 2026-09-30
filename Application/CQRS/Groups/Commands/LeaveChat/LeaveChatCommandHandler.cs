using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class LeaveChatCommandHandler : IRequestHandler<LeaveChatCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public LeaveChatCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(LeaveChatCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");

            // A direct chat has no OwnerId, so the owner-check below can't catch
            // it — guard separately, or a party could "leave" their own 1:1 chat
            // and leave a 1-member row behind that FindDirectChatAsync can no
            // longer match, orphaning it instead of reusing it on the next message.
            if (!chat.IsGroup)
                throw new InvalidOperationException("Cannot leave a direct chat.");

            // The owner can't just walk away — there'd be no one left who can
            // manage or delete the group. Client already hides "Leave" for the
            // owner (only "Delete" is shown), this keeps the server consistent
            // with that in case of a direct API call.
            if (chat.OwnerId == request.UserId)
                throw new InvalidOperationException("The owner cannot leave the chat. Delete it instead.");

            var isMember = await _chats.IsMemberAsync(request.ChatId, request.UserId);
            if (!isMember) return MediatR.Unit.Value; // idempotent

            await _chats.RemoveMemberAsync(request.ChatId, request.UserId);

            var groupId = request.ChatId.ToString();
            try { await _notifier.RemoveUserFromGroupAsync(request.UserId, groupId); } catch { }
            try { await _notifier.NotifyGroupAsync(groupId, "GroupMemberRemoved", new { UserId = request.UserId }); } catch { }

            return MediatR.Unit.Value;
        }
    }
}
