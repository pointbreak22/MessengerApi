using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public abstract class ApiControllerBase : ControllerBase
    {
        // Порядок должен точно совпадать с WebAPI.Services.CustomUserIdProvider —
        // иначе id пользователя в REST и в SignalR (Context.UserIdentifier) разъедутся.
        protected string GetCurrentUserId() =>
            User.FindFirst("sub")?.Value
            ?? User.FindFirst("oid")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;

        // Логин пользователя. Порядок обязан совпадать с WebAPI.Auth.SuperAdminHandler:
        // по этому значению определяется суперадмин, и расхождение означало бы, что
        // права выдаются не тому, кого пускает политика.
        protected string? GetCurrentUserEmail() =>
            User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value
            ?? User.FindFirst("emails")?.Value;

        // Имя для авто-создания профиля при первом входе через CIAM — берём первое,
        // что реально присутствует в токене (набор claims зависит от user flow).
        protected string GetCurrentUserName()
        {
            var name = User.FindFirst("name")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("emails")?.Value;

            if (!string.IsNullOrWhiteSpace(name)) return name;

            var id = GetCurrentUserId();
            return id.Length > 0 ? $"User-{id[..Math.Min(8, id.Length)]}" : "User";
        }
    }
}
