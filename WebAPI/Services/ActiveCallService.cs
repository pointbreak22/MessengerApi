using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace WebAPI.Services
{
    public record ActiveCallInfo(string UserId, string ChatId, string CallId, bool IsVideo);

    // Tracks which call room each user currently sits in — for presence
    // badges (red = in a call with me, yellow = in some other call) and the
    // "ongoing call, tap to join" affordance on a group. Mirrors IPresenceService's
    // shape/lifecycle, but is a separate concept: presence is "online at all",
    // this is "in a specific call room right now".
    public interface IActiveCallService
    {
        Task JoinAsync(string callId, string chatId, string userId, bool isVideo);

        // Returns true when the room has no members left after this leave.
        Task<bool> LeaveAsync(string callId, string userId);

        Task<IReadOnlyCollection<string>> GetParticipantsAsync(string callId);
        Task<(string ChatId, bool IsVideo)?> GetCallMetaAsync(string callId);
        Task<(string ChatId, string CallId, bool IsVideo)?> GetUserCallAsync(string userId);
        Task<IReadOnlyCollection<ActiveCallInfo>> GetAllActiveAsync();
    }

    // Redis-backed — survives multi-instance scale-out, same as PresenceService.
    public class RedisActiveCallService : IActiveCallService
    {
        private readonly IConnectionMultiplexer _redis;
        private const string RoomMembersPrefix = "call:room:members:";
        private const string RoomMetaPrefix = "call:room:meta:";
        private const string UserCallPrefix = "call:user:";
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

        public RedisActiveCallService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task JoinAsync(string callId, string chatId, string userId, bool isVideo)
        {
            var db = _redis.GetDatabase();
            var membersKey = RoomMembersPrefix + callId;
            var metaKey = RoomMetaPrefix + callId;

            await db.SetAddAsync(membersKey, userId);
            await db.KeyExpireAsync(membersKey, Ttl);
            await db.HashSetAsync(metaKey, new HashEntry[]
            {
                new("chatId", chatId),
                new("isVideo", isVideo ? "1" : "0"),
            });
            await db.KeyExpireAsync(metaKey, Ttl);
            await db.StringSetAsync(UserCallPrefix + userId, $"{callId}|{chatId}|{(isVideo ? "1" : "0")}", Ttl);
        }

        public async Task<bool> LeaveAsync(string callId, string userId)
        {
            var db = _redis.GetDatabase();
            var membersKey = RoomMembersPrefix + callId;

            await db.SetRemoveAsync(membersKey, userId);
            await db.KeyDeleteAsync(UserCallPrefix + userId);

            var remaining = await db.SetLengthAsync(membersKey);
            if (remaining == 0)
            {
                await db.KeyDeleteAsync(membersKey);
                await db.KeyDeleteAsync(RoomMetaPrefix + callId);
                return true;
            }
            return false;
        }

        public async Task<IReadOnlyCollection<string>> GetParticipantsAsync(string callId)
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(RoomMembersPrefix + callId);
            return members.Select(m => (string)m!).ToArray();
        }

        public async Task<(string ChatId, bool IsVideo)?> GetCallMetaAsync(string callId)
        {
            var db = _redis.GetDatabase();
            var entries = await db.HashGetAllAsync(RoomMetaPrefix + callId);
            if (entries.Length == 0) return null;
            var dict = entries.ToDictionary(e => (string)e.Name!, e => (string)e.Value!);
            return ((string ChatId, bool IsVideo)?)(dict.GetValueOrDefault("chatId", ""), dict.GetValueOrDefault("isVideo") == "1");
        }

        public async Task<(string ChatId, string CallId, bool IsVideo)?> GetUserCallAsync(string userId)
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(UserCallPrefix + userId);
            if (value.IsNullOrEmpty) return null;
            var parts = ((string)value!).Split('|');
            if (parts.Length != 3) return null;
            return ((string ChatId, string CallId, bool IsVideo)?)(parts[1], parts[0], parts[2] == "1");
        }

        public async Task<IReadOnlyCollection<ActiveCallInfo>> GetAllActiveAsync()
        {
            // SCAN over call:user:* — fine at this app's scale; a dedicated
            // index set would be the next step if concurrent-call volume ever
            // gets large enough for SCAN to matter.
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var result = new List<ActiveCallInfo>();
            await foreach (var key in server.KeysAsync(pattern: UserCallPrefix + "*"))
            {
                var value = await db.StringGetAsync(key);
                if (value.IsNullOrEmpty) continue;
                var parts = ((string)value!).Split('|');
                if (parts.Length != 3) continue;
                var userId = ((string)key!).Substring(UserCallPrefix.Length);
                result.Add(new ActiveCallInfo(userId, parts[1], parts[0], parts[2] == "1"));
            }
            return result;
        }
    }
}
