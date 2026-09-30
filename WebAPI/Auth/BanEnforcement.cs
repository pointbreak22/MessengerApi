using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Services;

namespace WebAPI.Auth
{
    /// <summary>
    /// Отсекает забаненного пользователя от всего API одним местом, а не проверкой
    /// в каждом контроллере — новый endpoint нельзя забыть закрыть. Стоит после
    /// UseAuthentication, так что покрывает и REST, и negotiate SignalR (новое
    /// подключение к хабу забаненный установить не сможет).
    ///
    /// Ответ — 403 с кодом account_banned: клиент по нему показывает экран
    /// "аккаунт заблокирован", а не пытается перелогиниться.
    /// </summary>
    public sealed class BanEnforcementMiddleware
    {
        private readonly RequestDelegate _next;

        public BanEnforcementMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, BanStatusService bans)
        {
            var userId = ReadUserId(context.User);
            if (!string.IsNullOrEmpty(userId) && await bans.IsBannedAsync(userId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = BanStatusService.BannedErrorCode,
                    message = "Аккаунт заблокирован администратором."
                });
                return;
            }

            await _next(context);
        }

        // Тот же порядок, что в ApiControllerBase.GetCurrentUserId и CustomUserIdProvider.
        internal static string? ReadUserId(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true) return null;
            return principal.FindFirst("sub")?.Value
                ?? principal.FindFirst("oid")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }

    /// <summary>
    /// Уже открытое WebSocket-соединение middleware не видит, поэтому каждый вызов
    /// метода хаба (отправка сообщения, "печатает", звонки) проверяется здесь.
    /// </summary>
    public sealed class BanHubFilter : IHubFilter
    {
        private readonly BanStatusService _bans;

        public BanHubFilter(BanStatusService bans) => _bans = bans;

        public async ValueTask<object?> InvokeMethodAsync(
            HubInvocationContext invocationContext,
            Func<HubInvocationContext, ValueTask<object?>> next)
        {
            var userId = invocationContext.Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId) && await _bans.IsBannedAsync(userId))
                throw new HubException(BanStatusService.BannedErrorCode);

            return await next(invocationContext);
        }

        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            var userId = context.Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId) && await _bans.IsBannedAsync(userId))
            {
                context.Context.Abort();
                return;
            }

            await next(context);
        }
    }
}
