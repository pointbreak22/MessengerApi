using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Messages.Commands
{
    public class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand, MediatR.Unit>
    {
        private readonly IMessageRepository _messages;
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public DeleteMessageCommandHandler(IMessageRepository messages, IChatRepository chats, IRealtimeNotifier notifier)
        {
            _messages = messages;
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
        {
            var message = await _messages.GetByIdAsync(request.MessageId);
            if (message == null) throw new KeyNotFoundException("Message not found");

            if (message.SenderId != request.RequesterId)
                throw new UnauthorizedAccessException("Only the sender can delete this message");

            var chatId = message.ChatId;
            var chat = await _chats.GetByIdAsync(chatId);

            await _messages.DeleteAsync(message);

            if (chat != null)
            {
                foreach (var member in chat.Members)
                {
                    try
                    {
                        await _notifier.NotifyUserAsync(member.UserId, "MessageDeleted", new
                        {
                            ChatId = chatId,
                            MessageId = request.MessageId
                        });
                    }
                    catch { }
                }
            }

            return MediatR.Unit.Value;
        }
    }
}
