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
    public class EfMessageRepository : IMessageRepository
    {
        private readonly ApplicationDbContext _db;
        public EfMessageRepository(ApplicationDbContext db) => _db = db;

        public async Task AddAsync(Message message)
        {
            await _db.Messages.AddAsync(message);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<Message>> GetMessagesAsync(Guid chatId, int limit = 50, DateTime? before = null)
        {
            IQueryable<Message> q = _db.Messages
                .Where(m => m.ChatId == chatId && !_db.Users.Any(u => u.Id == m.SenderId && u.IsBanned))
                .OrderByDescending(m => m.CreatedAt);
            if (before.HasValue)
            {
                q = q.Where(m => m.CreatedAt < before.Value);
            }
            return await q.Take(limit).ToListAsync();
        }

        public async Task<IEnumerable<Message>> SearchAsync(string userId, string search, int limit = 20)
        {
            var s = search.Trim().ToLower();
            var chatIds = _db.ChatMembers.Where(m => m.UserId == userId).Select(m => m.ChatId);

            return await _db.Messages
                .Where(m => chatIds.Contains(m.ChatId) && m.Text.ToLower().Contains(s)
                    && !_db.Users.Any(u => u.Id == m.SenderId && u.IsBanned))
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<Message?> GetByIdAsync(Guid id)
        {
            return await _db.Messages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task UpdateAsync(Message message)
        {
            _db.Messages.Update(message);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Message message)
        {
            _db.Messages.Remove(message);
            await _db.SaveChangesAsync();
        }
    }
}
