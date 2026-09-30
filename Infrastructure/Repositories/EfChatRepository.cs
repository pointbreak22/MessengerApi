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
    public class EfChatRepository : IChatRepository
    {
        private readonly ApplicationDbContext _db;
        public EfChatRepository(ApplicationDbContext db) => _db = db;

        public async Task<Chat?> GetByIdAsync(Guid id)
        {
            return await _db.Chats.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task AddAsync(Chat chat)
        {
            await _db.Chats.AddAsync(chat);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Chat chat)
        {
            _db.Chats.Update(chat);
            await _db.SaveChangesAsync();
        }

        public async Task AddMemberAsync(ChatMember member)
        {
            await _db.ChatMembers.AddAsync(member);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(Guid chatId, string userId)
        {
            var member = await _db.ChatMembers.FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId);
            if (member == null) return;
            _db.ChatMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Chat chat)
        {
            // Messages has no FK to Chats (unlike ChatMembers, which cascades),
            // so it needs an explicit cleanup or it'd orphan the rows.
            var messages = await _db.Messages.Where(m => m.ChatId == chat.Id).ToListAsync();
            if (messages.Count > 0) _db.Messages.RemoveRange(messages);

            _db.Chats.Remove(chat);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<Chat>> GetDirectChatsForUserAsync(string userId)
        {
            return await _db.Chats
                .Where(c => !c.IsGroup && c.Members.Any(m => m.UserId == userId))
                .ToListAsync();
        }

        public async Task<IEnumerable<Chat>> GetAllChatsForUserAsync(string userId)
        {
            return await _db.Chats
                .Include(c => c.Members)
                .Where(c => c.Members.Any(m => m.UserId == userId))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> IsMemberAsync(Guid chatId, string userId)
        {
            return await _db.ChatMembers.AnyAsync(m => m.ChatId == chatId && m.UserId == userId);
        }

        public async Task<Chat?> FindDirectChatAsync(string userIdA, string userIdB)
        {
            return await _db.Chats
                .Include(c => c.Members)
                .Where(c => !c.IsGroup
                    && c.Members.Any(m => m.UserId == userIdA)
                    && c.Members.Any(m => m.UserId == userIdB))
                .FirstOrDefaultAsync();
        }

        public async Task MarkAsReadAsync(Guid chatId, string userId)
        {
            var member = await _db.ChatMembers
                .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId);
            if (member == null) return;
            member.MarkRead();
            _db.ChatMembers.Update(member);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsAsync(
            IEnumerable<Guid> chatIds, string userId)
        {
            var idList = chatIds.ToList();

            var counts = await _db.ChatMembers
                .Where(m => m.UserId == userId && idList.Contains(m.ChatId))
                .Select(m => new
                {
                    m.ChatId,
                    UnreadCount = _db.Messages.Count(msg =>
                        msg.ChatId == m.ChatId &&
                        (m.LastReadAt == null || msg.CreatedAt > m.LastReadAt) &&
                        !_db.Users.Any(u => u.Id == msg.SenderId && u.IsBanned))
                })
                .ToListAsync();

            return counts.ToDictionary(x => x.ChatId, x => x.UnreadCount);
        }

        public async Task<(IEnumerable<Chat> Items, int Total)> GetPublicGroupsAsync(int page, int pageSize, string? search)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.Chats.Where(c => c.IsGroup && c.IsPublic);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c => c.Name != null && c.Name.ToLower().Contains(s));
            }

            var total = await query.CountAsync();
            var items = await query
                .Include(c => c.Members)
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }
    }
}
