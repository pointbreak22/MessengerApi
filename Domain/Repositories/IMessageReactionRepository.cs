using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Repositories
{
    public interface IMessageReactionRepository
    {
        Task<MessageReaction?> GetAsync(Guid messageId, string userId);
        Task AddAsync(MessageReaction reaction);
        Task UpdateAsync(MessageReaction reaction);
        Task RemoveAsync(MessageReaction reaction);
        Task<IReadOnlyList<MessageReaction>> GetForMessageAsync(Guid messageId);
        Task<IReadOnlyList<MessageReaction>> GetForMessagesAsync(IEnumerable<Guid> messageIds);
    }
}
