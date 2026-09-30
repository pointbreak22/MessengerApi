using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    using Domain.Entities;

    public interface IMessageRepository
    {
        Task AddAsync(Message message);
        Task<IEnumerable<Message>> GetMessagesAsync(Guid chatId, int limit = 50, DateTime? before = null);
        // Полнотекстовый поиск по всем чатам, где userId состоит участником.
        Task<IEnumerable<Message>> SearchAsync(string userId, string search, int limit = 20);
        Task<Message?> GetByIdAsync(Guid id);
        Task UpdateAsync(Message message);
        Task DeleteAsync(Message message);
    }
}
