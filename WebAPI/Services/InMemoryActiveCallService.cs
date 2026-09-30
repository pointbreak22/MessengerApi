using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Services
{
    // Fallback when Redis isn't configured — unlike NullPresenceService (a true
    // no-op), this actually works: group-call presence needs *some* shared
    // state to function at all, and in-memory is correct for the single-instance
    // deploy this app runs today. Doesn't survive multi-instance scale-out —
    // same caveat presence already has when Redis is absent, just made functional
    // instead of silently doing nothing.
    public class InMemoryActiveCallService : IActiveCallService
    {
        private record Room(string ChatId, bool IsVideo, HashSet<string> Members);

        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly ConcurrentDictionary<string, (string ChatId, string CallId, bool IsVideo)> _userCalls = new();
        private readonly object _lock = new();

        public Task JoinAsync(string callId, string chatId, string userId, bool isVideo)
        {
            lock (_lock)
            {
                var room = _rooms.GetOrAdd(callId, _ => new Room(chatId, isVideo, new HashSet<string>()));
                room.Members.Add(userId);
                _userCalls[userId] = (chatId, callId, isVideo);
            }
            return Task.CompletedTask;
        }

        public Task<bool> LeaveAsync(string callId, string userId)
        {
            lock (_lock)
            {
                _userCalls.TryRemove(userId, out _);
                if (!_rooms.TryGetValue(callId, out var room)) return Task.FromResult(true);

                room.Members.Remove(userId);
                if (room.Members.Count == 0)
                {
                    _rooms.TryRemove(callId, out _);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
        }

        public Task<IReadOnlyCollection<string>> GetParticipantsAsync(string callId)
        {
            IReadOnlyCollection<string> members = _rooms.TryGetValue(callId, out var room)
                ? room.Members.ToArray()
                : Array.Empty<string>();
            return Task.FromResult(members);
        }

        public Task<(string ChatId, bool IsVideo)?> GetCallMetaAsync(string callId)
        {
            if (_rooms.TryGetValue(callId, out var room))
                return Task.FromResult<(string ChatId, bool IsVideo)?>((room.ChatId, room.IsVideo));
            return Task.FromResult<(string ChatId, bool IsVideo)?>(null);
        }

        public Task<(string ChatId, string CallId, bool IsVideo)?> GetUserCallAsync(string userId)
        {
            if (_userCalls.TryGetValue(userId, out var call))
                return Task.FromResult<(string ChatId, string CallId, bool IsVideo)?>(call);
            return Task.FromResult<(string ChatId, string CallId, bool IsVideo)?>(null);
        }

        public Task<IReadOnlyCollection<ActiveCallInfo>> GetAllActiveAsync()
        {
            IReadOnlyCollection<ActiveCallInfo> all = _userCalls
                .Select(kv => new ActiveCallInfo(kv.Key, kv.Value.ChatId, kv.Value.CallId, kv.Value.IsVideo))
                .ToArray();
            return Task.FromResult(all);
        }
    }
}
