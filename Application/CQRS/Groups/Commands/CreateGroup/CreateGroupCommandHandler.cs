using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;
using Domain.Entities;

namespace Application.CQRS.Groups.Commands
{
    public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, System.Guid>
    {
        private readonly IChatRepository _chats;
        private readonly IOutboxRepository _outbox; // not used but could be
        private readonly IRealtimeNotifier _notifier;

        public CreateGroupCommandHandler(IChatRepository chats, IOutboxRepository outbox, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _outbox = outbox;
            _notifier = notifier;
        }

        public async Task<System.Guid> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
        {
            var chat = Chat.CreateGroup(request.Name, request.OwnerId, request.IsPublic);
            await _chats.AddAsync(chat);

            // add owner
            await _chats.AddMemberAsync(ChatMember.Create(chat.Id, request.OwnerId));

            if (request.MemberIds != null)
            {
                foreach (var m in request.MemberIds)
                {
                    await _chats.AddMemberAsync(ChatMember.Create(chat.Id, m));
                    // notify each added member
                    try { await _notifier.NotifyUserAsync(m, "AddedToGroup", new { ChatId = chat.Id, ChatName = chat.Name }); } catch { }
                }
            }

            return chat.Id;
        }
    }
}
