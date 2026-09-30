using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Entities;
using Domain.Repositories;
using Application.Common;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Commands
{
    public class ToggleReactionCommandHandler : IRequestHandler<ToggleReactionCommand, IReadOnlyList<MessageReactionDto>>
    {
        private readonly IMessageRepository _messages;
        private readonly IMessageReactionRepository _reactions;
        private readonly IChatRepository _chats;
        private readonly IRealtimeNotifier _notifier;

        public ToggleReactionCommandHandler(
            IMessageRepository messages,
            IMessageReactionRepository reactions,
            IChatRepository chats,
            IRealtimeNotifier notifier)
        {
            _messages = messages;
            _reactions = reactions;
            _chats = chats;
            _notifier = notifier;
        }

        public async Task<IReadOnlyList<MessageReactionDto>> Handle(ToggleReactionCommand request, CancellationToken cancellationToken)
        {
            var message = await _messages.GetByIdAsync(request.MessageId);
            if (message == null) throw new KeyNotFoundException("Message not found");

            var existing = await _reactions.GetAsync(request.MessageId, request.RequesterId);
            if (existing == null)
            {
                await _reactions.AddAsync(MessageReaction.Create(request.MessageId, request.RequesterId, request.Emoji));
            }
            else if (existing.Emoji == request.Emoji)
            {
                // Same emoji tapped again — toggle off.
                await _reactions.RemoveAsync(existing);
            }
            else
            {
                existing.UpdateEmoji(request.Emoji);
                await _reactions.UpdateAsync(existing);
            }

            var current = await _reactions.GetForMessageAsync(request.MessageId);
            var summary = current.Select(r => new MessageReactionDto(r.Emoji, r.UserId)).ToList();

            var chat = await _chats.GetByIdAsync(message.ChatId);
            if (chat != null)
            {
                // Per-member, not Clients.Group — same reasoning as MessageEdited.
                foreach (var member in chat.Members)
                {
                    try
                    {
                        await _notifier.NotifyUserAsync(member.UserId, "MessageReactionsChanged", new
                        {
                            ChatId = message.ChatId,
                            MessageId = message.Id,
                            Reactions = summary
                        });
                    }
                    catch { }
                }
            }

            return summary;
        }
    }
}
