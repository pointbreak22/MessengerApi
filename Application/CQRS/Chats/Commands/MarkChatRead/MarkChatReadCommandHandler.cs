using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;

namespace Application.CQRS.Chats.Commands
{
    public class MarkChatReadCommandHandler : IRequestHandler<MarkChatReadCommand, Unit>
    {
        private readonly IChatRepository _chats;

        public MarkChatReadCommandHandler(IChatRepository chats)
        {
            _chats = chats;
        }

        public async Task<Unit> Handle(MarkChatReadCommand request, CancellationToken cancellationToken)
        {
            await _chats.MarkAsReadAsync(request.ChatId, request.UserId);
            return Unit.Value;
        }
    }
}
