using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IIpBanRepository
    {
        Task<bool> IsBannedAsync(string ipAddress, DateTime now);
        Task<IReadOnlyList<IpBan>> GetActiveAsync(DateTime now);
        Task<IpBan?> GetByIdAsync(Guid id);
        Task AddAsync(IpBan ban);
        Task RemoveAsync(IpBan ban);
    }
}
