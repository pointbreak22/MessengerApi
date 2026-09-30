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
    public class EfMessageReactionRepository : IMessageReactionRepository
    {
        private readonly ApplicationDbContext _db;
        public EfMessageReactionRepository(ApplicationDbContext db) => _db = db;

        public async Task<MessageReaction?> GetAsync(Guid messageId, string userId)
        {
            return await _db.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId);
        }

        public async Task AddAsync(MessageReaction reaction)
        {
            await _db.MessageReactions.AddAsync(reaction);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(MessageReaction reaction)
        {
            _db.MessageReactions.Update(reaction);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(MessageReaction reaction)
        {
            _db.MessageReactions.Remove(reaction);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<MessageReaction>> GetForMessageAsync(Guid messageId)
        {
            return await _db.MessageReactions.Where(r => r.MessageId == messageId).ToListAsync();
        }

        public async Task<IReadOnlyList<MessageReaction>> GetForMessagesAsync(IEnumerable<Guid> messageIds)
        {
            return await _db.MessageReactions.Where(r => messageIds.Contains(r.MessageId)).ToListAsync();
        }
    }
}
