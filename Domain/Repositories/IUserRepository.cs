using System.Threading.Tasks;

namespace Domain.Repositories
{
    using Domain.Entities;

    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(string id);
        Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<string> ids);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        // Забаненные пользователи в выдачу не попадают, кроме includeBanned (админка).
        Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null, string? sortBy = null, bool? isOnline = null, bool includeBanned = false);
        Task<IEnumerable<User>> GetFriendsAsync(string userId);
        Task<bool> IsBannedAsync(string userId);
        // Какие из переданных id принадлежат забаненным — для фильтрации участников чатов и заявок.
        Task<HashSet<string>> GetBannedIdsAsync(IEnumerable<string> ids);
    }
}
