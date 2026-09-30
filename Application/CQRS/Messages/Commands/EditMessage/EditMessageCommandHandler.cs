using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.Common;

namespace Application.CQRS.Messages.Commands
{
    public class EditMessageCommandHandler : IRequestHandler<EditMessageCommand, MediatR.Unit>
    {
        private readonly IMessageRepository _messages;
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public EditMessageCommandHandler(IMessageRepository messages, IChatRepository chats, IRealtimeNotifier notifier)
        {
            _messages = messages;
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<MediatR.Unit> Handle(EditMessageCommand request, CancellationToken cancellationToken)
        {
            var message = await _messages.GetByIdAsync(request.MessageId);
            if (message == null) throw new KeyNotFoundException("Message not found");

            // Own messages only — no moderator/owner override, per the request.
            if (message.SenderId != request.RequesterId)
                throw new UnauthorizedAccessException("Only the sender can edit this message");

            message.UpdateText(request.Text);
            await _messages.UpdateAsync(message);

            var chat = await _chats.GetByIdAsync(message.ChatId);
            if (chat != null)
            {
                // Per-member, not Clients.Group — reaches everyone regardless
                // of which chat they currently have open (see OutboxDispatcher's
                // NewMessage fix for the same reasoning).
                foreach (var member in chat.Members)
                {
                    try
                    {
                        await _notifier.NotifyUserAsync(member.UserId, "MessageEdited", new
                        {
                            ChatId = message.ChatId,
                            MessageId = message.Id,
                            Text = message.Text,
                            EditedAt = message.EditedAt
                        });
                    }
                    catch { }
                }
            }

            return MediatR.Unit.Value;
        }
    }
}
