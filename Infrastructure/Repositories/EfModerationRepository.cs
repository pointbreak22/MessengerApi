using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class EfModerationRepository : IModerationRepository
    {
        private readonly ApplicationDbContext _db;
        public EfModerationRepository(ApplicationDbContext db) => _db = db;

        public async Task RecordViolationAsync(User user, ModerationViolation violation, IEnumerable<OutboxMessage> notifications)
        {
            _db.Users.Update(user);
            await _db.ModerationViolations.AddAsync(violation);
            await _db.OutboxMessages.AddRangeAsync(notifications);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ModerationViolation>> GetByUserAsync(string userId, int take)
        {
            return await _db.ModerationViolations
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Take(take)
                .ToListAsync();
        }
    }
}
