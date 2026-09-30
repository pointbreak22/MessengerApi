using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Application.CQRS.Messages.DTOs;

namespace Application.CQRS.Messages.Queries
{
    public class SearchMessagesQueryHandler : IRequestHandler<SearchMessagesQuery, IReadOnlyList<MessageDto>>
    {
        private readonly IMessageRepository _messages;

        public SearchMessagesQueryHandler(IMessageRepository messages)
        {
            _messages = messages;
        }

        public async Task<IReadOnlyList<MessageDto>> Handle(SearchMessagesQuery request, CancellationToken cancellationToken)
        {
            var messages = await _messages.SearchAsync(request.UserId, request.Search, request.Limit);
            return messages.Select(m => MessageDto.FromEntity(m)).ToList();
        }
    }
}
