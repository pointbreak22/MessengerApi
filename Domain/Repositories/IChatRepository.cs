using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Domain.Repositories
{
    using Domain.Entities;

    public interface IChatRepository
    {
        Task<Chat?> GetByIdAsync(Guid id);
        Task AddAsync(Chat chat);
        Task UpdateAsync(Chat chat);
        Task AddMemberAsync(ChatMember member);
        Task RemoveMemberAsync(Guid chatId, string userId);
        Task DeleteAsync(Chat chat);
        Task<IEnumerable<Chat>> GetDirectChatsForUserAsync(string userId);
        Task<IEnumerable<Chat>> GetAllChatsForUserAsync(string userId);
        Task<bool> IsMemberAsync(Guid chatId, string userId);
        Task<Chat?> FindDirectChatAsync(string userIdA, string userIdB);
        Task MarkAsReadAsync(Guid chatId, string userId);
        Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsAsync(IEnumerable<Guid> chatIds, string userId);
        Task<(IEnumerable<Chat> Items, int Total)> GetPublicGroupsAsync(int page, int pageSize, string? search);
    }
}
