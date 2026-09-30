using System;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace Domain.Repositories
{
    using Domain.Entities;

    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage message);
        Task<Guid> AddMessageAndOutboxAsync(Message message, OutboxMessage outbox);
        Task<IEnumerable<OutboxMessage>> GetPendingAsync(int limit = 50);
        Task MarkSentAsync(OutboxMessage message);
        Task<OutboxMessage?> FindByIdempotencyKeyAsync(string idempotencyKey);
        Task UpdateAsync(OutboxMessage message);
    }
}
