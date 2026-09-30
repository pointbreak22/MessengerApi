using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Services
{
    // Берёт идентификатор пользователя из claim "sub" (OAuth2) или ClaimTypes.NameIdentifier
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            var user = connection.User;
            if (user == null) return null;

            // Порядок должен точно совпадать с WebAPI.Controllers.ApiControllerBase.GetCurrentUserId() —
            // иначе Context.UserIdentifier в SignalR не совпадёт с userId, которым помечены записи в БД
            // (FriendId и т.д.), и NotifyUserAsync("FriendRequest", ...) не найдёт подключение получателя.
            var sub = user.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub)) return sub;

            var oid = user.FindFirst("oid")?.Value;
            if (!string.IsNullOrEmpty(oid)) return oid;

            var nameId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(nameId)) return nameId;

            return null;
        }
    }
}
