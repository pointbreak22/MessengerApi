using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class EfUserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _db;
        public EfUserRepository(ApplicationDbContext db) => _db = db;

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            return await _db.Users.Where(u => idList.Contains(u.Id) && !u.IsBanned).ToListAsync();
        }

        public async Task<bool> IsBannedAsync(string userId)
        {
            return await _db.Users.AnyAsync(u => u.Id == userId && u.IsBanned);
        }

        public async Task<HashSet<string>> GetBannedIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return new HashSet<string>();

            var banned = await _db.Users
                .Where(u => u.IsBanned && idList.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
            return banned.ToHashSet();
        }

        public async Task AddAsync(User user)
        {
            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null, string? sortBy = null, bool? isOnline = null, bool includeBanned = false)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.Users.AsQueryable();

            if (!includeBanned)
            {
                query = query.Where(u => !u.IsBanned);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.UserName.ToLower().Contains(s));
            }

            if (isOnline.HasValue)
            {
                query = query.Where(u => u.IsOnline == isOnline.Value);
            }

            // Sorting
            query = sortBy?.ToLower() switch
            {
                "lastseen" => query.OrderByDescending(u => u.LastSeenAt),
                "username_desc" => query.OrderByDescending(u => u.UserName),
                _ => query.OrderBy(u => u.UserName),
            };

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
        }

        public async Task<IEnumerable<User>> GetFriendsAsync(string userId)
        {
            // Join via Friendship table (accepted)
            var friendIds = await _db.Friendships
                .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == Domain.Enums.FriendshipStatus.Accepted)
                .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
                .ToListAsync();

            return await _db.Users.Where(u => friendIds.Contains(u.Id) && !u.IsBanned).ToListAsync();
        }
    }
}
