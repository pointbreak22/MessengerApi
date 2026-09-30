using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Domain.Repositories
{
    using Domain.Entities;

    public interface IFriendshipRepository
    {
        Task<Friendship> CreateRequestAsync(string userId, string friendId);
        Task AcceptRequestAsync(Guid friendshipId);
        Task RemoveFriendAsync(Guid friendshipId);
        Task<IEnumerable<Friendship>> GetFriendshipsForUserAsync(string userId);
        Task<Friendship?> GetByIdAsync(Guid id);
        Task<Friendship?> FindRequestAsync(string userId, string friendId);
    }
}
