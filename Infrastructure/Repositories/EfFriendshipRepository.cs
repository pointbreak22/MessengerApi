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
    public class EfFriendshipRepository : IFriendshipRepository
    {
        private readonly ApplicationDbContext _db;
        public EfFriendshipRepository(ApplicationDbContext db) => _db = db;

        public async Task<Friendship> CreateRequestAsync(string userId, string friendId)
        {
            var existing = await FindRequestAsync(userId, friendId);
            if (existing != null) return existing;

            var f = Friendship.CreateRequest(userId, friendId);
            await _db.Friendships.AddAsync(f);
            await _db.SaveChangesAsync();
            return f;
        }

        public async Task AcceptRequestAsync(Guid friendshipId)
        {
            var f = await _db.Friendships.FindAsync(friendshipId);
            if (f == null) return;
            f.Accept();
            _db.Friendships.Update(f);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveFriendAsync(Guid friendshipId)
        {
            var f = await _db.Friendships.FindAsync(friendshipId);
            if (f == null) return;
            _db.Friendships.Remove(f);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<Friendship>> GetFriendshipsForUserAsync(string userId)
        {
            return await _db.Friendships.Where(f => f.UserId == userId || f.FriendId == userId).ToListAsync();
        }

        public async Task<Friendship?> GetByIdAsync(Guid id)
        {
            return await _db.Friendships.FindAsync(id);
        }

        public async Task<Friendship?> FindRequestAsync(string userId, string friendId)
        {
            return await _db.Friendships.FirstOrDefaultAsync(f => (f.UserId == userId && f.FriendId == friendId) || (f.UserId == friendId && f.FriendId == userId));
        }
    }
}
