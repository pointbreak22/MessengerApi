using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace WebAPI.Services
{
    public interface IPresenceService
    {
        Task AddConnectionAsync(string userId, string connectionId);
        Task<bool> RemoveConnectionAsync(string userId, string connectionId); // returns true when user is now fully offline
        Task<IReadOnlyCollection<string>> GetConnectionsAsync(string userId);
    }

    public class PresenceService : IPresenceService
    {
        private readonly IConnectionMultiplexer _redis;
        private const string Prefix = "presence:";

        public PresenceService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task AddConnectionAsync(string userId, string connectionId)
        {
            var db = _redis.GetDatabase();
            var key = Prefix + userId;
            await db.SetAddAsync(key, connectionId);
            await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            var sub = _redis.GetSubscriber();
            await sub.PublishAsync(RedisChannel.Literal("presence:changes"), $"online:{userId}");
        }

        public async Task<bool> RemoveConnectionAsync(string userId, string connectionId)
        {
            var db = _redis.GetDatabase();
            var key = Prefix + userId;
            await db.SetRemoveAsync(key, connectionId);
            var remaining = await db.SetLengthAsync(key);
            if (remaining == 0)
            {
                await db.KeyDeleteAsync(key);
                var sub = _redis.GetSubscriber();
                await sub.PublishAsync(RedisChannel.Literal("presence:changes"), $"offline:{userId}");
                return true;
            }
            return false;
        }

        public async Task<IReadOnlyCollection<string>> GetConnectionsAsync(string userId)
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(Prefix + userId);
            return members.Select(m => (string)m!).ToArray();
        }
    }
}
