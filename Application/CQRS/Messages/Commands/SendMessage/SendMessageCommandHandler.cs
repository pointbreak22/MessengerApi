using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Repositories;
using Domain.Entities;

namespace Application.CQRS.Messages.Commands
{
    public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, System.Guid>
    {
        private readonly IMessageRepository _messageRepository;
        private readonly Domain.Repositories.IOutboxRepository? _outboxRepository;
        private readonly Domain.Repositories.IOutboxSignal? _outboxSignal;

        // outboxSignal is optional for the same reason outboxRepository is:
        // existing tests construct this handler directly with just the pieces
        // they care about.
        public SendMessageCommandHandler(IMessageRepository messageRepository, Domain.Repositories.IOutboxRepository? outboxRepository = null, Domain.Repositories.IOutboxSignal? outboxSignal = null)
        {
            _messageRepository = messageRepository;
            _outboxRepository = outboxRepository;
            _outboxSignal = outboxSignal;
        }

        public async Task<System.Guid> Handle(SendMessageCommand request, CancellationToken cancellationToken)
        {
            // Idempotency: если указан ключ, проверим существующие outbox записи
            if (!string.IsNullOrEmpty(request.IdempotencyKey) && _outboxRepository != null)
            {
                var existing = await _outboxRepository.FindByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existing != null)
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(existing.Payload);
                        if (doc.RootElement.TryGetProperty("MessageId", out var mid))
                        {
                            return mid.GetGuid();
                        }
                    }
                    catch { }
                }
            }

            // Сначала создаём сообщение и outbox и сохраняем атомарно через репозиторий Outbox
            var message = Message.Create(request.ChatId, request.SenderId, request.Text, request.AttachmentUrl, request.ReplyToMessageId);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { MessageId = message.Id, ChatId = request.ChatId, SenderId = request.SenderId, Text = request.Text, AttachmentUrl = request.AttachmentUrl, ReplyToMessageId = request.ReplyToMessageId });
            var outbox = Domain.Entities.OutboxMessage.Create("MessageCreated", payload, request.IdempotencyKey);

            if (_outboxRepository != null)
            {
                var savedId = await _outboxRepository.AddMessageAndOutboxAsync(message, outbox);
                // Poke the dispatcher so it picks this up immediately instead
                // of on its next scheduled poll. Best-effort by design — if it
                // is missed, the poll still delivers, just later.
                _outboxSignal?.Notify();
                return savedId;
            }

            // Fallback: сохраняем только сообщение
            await _messageRepository.AddAsync(message);
            return message.Id;
        }
    }
}
