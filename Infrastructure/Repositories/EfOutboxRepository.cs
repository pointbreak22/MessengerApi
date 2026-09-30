using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class EfOutboxRepository : IOutboxRepository
    {
        private readonly ApplicationDbContext _db;
        public EfOutboxRepository(ApplicationDbContext db) => _db = db;

        public async Task AddAsync(OutboxMessage message)
        {
            await _db.OutboxMessages.AddAsync(message);
            await _db.SaveChangesAsync();
        }

        public async Task<Guid> AddMessageAndOutboxAsync(Message message, OutboxMessage outbox)
        {
            await _db.Messages.AddAsync(message);
            await _db.OutboxMessages.AddAsync(outbox);
            await _db.SaveChangesAsync();
            return message.Id;
        }

        public async Task<IEnumerable<OutboxMessage>> GetPendingAsync(int limit = 50)
        {
            var now = DateTime.UtcNow;
            return await _db.OutboxMessages
                .Where(o => o.SentAt == null && o.DeadLetterAt == null && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
                .OrderBy(o => o.OccurredAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task MarkSentAsync(OutboxMessage message)
        {
            message.MarkSent();
            _db.OutboxMessages.Update(message);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(OutboxMessage message)
        {
            _db.OutboxMessages.Update(message);
            await _db.SaveChangesAsync();
        }

        public async Task<OutboxMessage?> FindByIdempotencyKeyAsync(string idempotencyKey)
        {
            return await _db.OutboxMessages.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        }
    }
}
