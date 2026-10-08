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
    public class EfIpBanRepository : IIpBanRepository
    {
        private readonly ApplicationDbContext _db;
        public EfIpBanRepository(ApplicationDbContext db) => _db = db;

        public async Task<bool> IsBannedAsync(string ipAddress, DateTime now)
        {
            return await _db.IpBans.AnyAsync(b => b.IpAddress == ipAddress && (b.ExpiresAt == null || b.ExpiresAt > now));
        }

        public async Task<IReadOnlyList<IpBan>> GetActiveAsync(DateTime now)
        {
            return await _db.IpBans
                .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<IpBan?> GetByIdAsync(Guid id)
        {
            return await _db.IpBans.FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task AddAsync(IpBan ban)
        {
            await _db.IpBans.AddAsync(ban);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(IpBan ban)
        {
            _db.IpBans.Remove(ban);
            await _db.SaveChangesAsync();
        }
    }
}
