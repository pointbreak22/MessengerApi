using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Groups.Commands
{
    public class UpdateChatAvatarCommandHandler : IRequestHandler<UpdateChatAvatarCommand, MediatR.Unit>
    {
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public UpdateChatAvatarCommandHandler(IChatRepository chats, IRealtimeNotifier notifier)
        {
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(UpdateChatAvatarCommand request, CancellationToken cancellationToken)
        {
            var chat = await _chats.GetByIdAsync(request.ChatId);
            if (chat == null) throw new KeyNotFoundException("Chat not found");
            if (!chat.IsGroup) throw new InvalidOperationException("Only groups have an avatar.");

            var requesterIsMember = await _chats.IsMemberAsync(request.ChatId, request.RequesterId);
            if (!requesterIsMember)
                throw new UnauthorizedAccessException("Only chat members can change the group avatar");

            chat.SetAvatarUrl(request.AvatarUrl);
            await _chats.UpdateAsync(chat);

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
