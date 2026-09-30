using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Application.Common;
using WebAPI.Hubs;

namespace WebAPI.Services
{
    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<ChatHub> _hub;
        private readonly IPresenceService _presence;

        public RealtimeNotifier(IHubContext<ChatHub> hub, IPresenceService presence)
        {
            _hub = hub;
            _presence = presence;
        }

        public async Task NotifyUserAsync(string userId, string eventName, object payload)
        {
            await _hub.Clients.User(userId).SendAsync(eventName, payload);
        }

        public async Task NotifyGroupAsync(string groupId, string eventName, object payload)
        {
            await _hub.Clients.Group(groupId).SendAsync(eventName, payload);
        }

        public async Task RemoveUserFromGroupAsync(string userId, string groupId)
        {
            var connectionIds = await _presence.GetConnectionsAsync(userId);
            foreach (var connectionId in connectionIds)
            {
                await _hub.Groups.RemoveFromGroupAsync(connectionId, groupId);
            }
        }
    }
}
