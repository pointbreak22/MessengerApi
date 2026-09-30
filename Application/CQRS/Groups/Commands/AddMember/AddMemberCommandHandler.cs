using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class AddMemberCommandHandler : IRequestHandler<AddMemberCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IUserRepository _users;
        private readonly IRealtimeNotifier _notifier;

        public AddMemberCommandHandler(IChatRepository chats, IUserRepository users, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _users = users;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(AddMemberCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");

            // A direct chat is exactly two people by construction (FindDirectChatAsync,
            // the 1:1 UI) — adding a third would silently break that invariant.
            if (!chat.IsGroup)
                throw new InvalidOperationException("Cannot add members to a direct chat.");

            // Invite model: any existing member can add someone else — for both
            // private and public groups. Self-join for public groups goes through
            // JoinGroupCommand instead; this is just the other way in.
            var requesterIsMember = await _chats.IsMemberAsync(request.ChatId, request.RequesterId);
            if (!requesterIsMember)
                throw new UnauthorizedAccessException("Only chat members can add members");

            var user = await _users.GetByIdAsync(request.UserIdToAdd);
            if (user == null) throw new KeyNotFoundException("User not found");

            await _chats.AddMemberAsync(Domain.Entities.ChatMember.Create(request.ChatId, request.UserIdToAdd));

            // notify user and group
            try { await _notifier.NotifyUserAsync(request.UserIdToAdd, "AddedToGroup", new { ChatId = request.ChatId }); } catch { }
            try { await _notifier.NotifyGroupAsync(request.ChatId.ToString(), "GroupMemberAdded", new { UserId = request.UserIdToAdd }); } catch { }

            return MediatR.Unit.Value;
        }
    }
}
