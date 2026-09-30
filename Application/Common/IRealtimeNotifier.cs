using System.Threading.Tasks;

namespace Application.Common
{
    public interface IRealtimeNotifier
    {
        Task NotifyUserAsync(string userId, string eventName, object payload);
        Task NotifyGroupAsync(string groupId, string eventName, object payload);

        // Force-unsubscribes every live connection of userId from the SignalR
        // group for groupId — needed when membership is revoked server-side
        // (removed/left/chat deleted), since the client can't be trusted to
        // call LeaveChatRoom itself and would otherwise keep receiving
        // NewMessage for a chat it no longer belongs to.
        Task RemoveUserFromGroupAsync(string userId, string groupId);
    }
}
