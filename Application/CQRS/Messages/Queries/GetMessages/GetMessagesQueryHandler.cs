using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Queries
{
    public class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, IReadOnlyList<MessageDto>>
    {
        private readonly IMessageRepository _messages;
        private readonly IMessageReactionRepository _reactions;

        public GetMessagesQueryHandler(IMessageRepository messages, IMessageReactionRepository reactions)
        {
            _messages = messages;
            _reactions = reactions;
        }

        public async Task<IReadOnlyList<MessageDto>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
        {
            var messages = (await _messages.GetMessagesAsync(request.ChatId, request.Limit, request.Before)).ToList();
            var reactions = await _reactions.GetForMessagesAsync(messages.Select(m => m.Id));
            var reactionsByMessage = reactions
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<MessageReactionDto>)g.Select(r => new MessageReactionDto(r.Emoji, r.UserId)).ToList());

            return messages
                .Select(m => MessageDto.FromEntity(m, reactionsByMessage.GetValueOrDefault(m.Id)))
                .ToList();
        }
    }
}
