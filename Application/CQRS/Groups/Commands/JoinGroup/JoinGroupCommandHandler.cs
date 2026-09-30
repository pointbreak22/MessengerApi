using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Entities;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class JoinGroupCommandHandler : IRequestHandler<JoinGroupCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public JoinGroupCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(JoinGroupCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");

            if (!chat.IsGroup || !chat.IsPublic)
                throw new UnauthorizedAccessException("Group is not public");

            var isMember = await _chats.IsMemberAsync(request.ChatId, request.UserId);
            if (!isMember)
            {
                await _chats.AddMemberAsync(ChatMember.Create(request.ChatId, request.UserId));
                try { await _notifier.NotifyGroupAsync(request.ChatId.ToString(), "GroupMemberAdded", new { UserId = request.UserId }); } catch { }
            }

            return MediatR.Unit.Value;
        }
    }
}
