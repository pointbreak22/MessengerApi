using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Domain.Entities;

namespace Application.CQRS.Chats.Commands
{
    public class CreateDirectChatCommandHandler : IRequestHandler<CreateDirectChatCommand, Guid>
    {
        private readonly IChatRepository _chats;

        public CreateDirectChatCommandHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public async Task<Guid> Handle(CreateDirectChatCommand request, CancellationToken cancellationToken)
        {
            var existing = await _chats.FindDirectChatAsync(request.UserId, request.TargetUserId);
            if (existing != null) return existing.Id;

            var chat = Chat.CreateDirect();
            await _chats.AddAsync(chat);
            await _chats.AddMemberAsync(ChatMember.Create(chat.Id, request.UserId));
            await _chats.AddMemberAsync(ChatMember.Create(chat.Id, request.TargetUserId));
            return chat.Id;
        }
    }
}
