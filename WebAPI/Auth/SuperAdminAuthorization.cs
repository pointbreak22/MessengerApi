using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Domain.Enums;
using Domain.Repositories;
using WebAPI.Services;

namespace WebAPI.Auth
{
    public sealed class SuperAdminRequirement : IAuthorizationRequirement
    {
        public const string PolicyName = "SuperAdmin";
    }

    /// <summary>
    /// Пропускает по любому из двух признаков — сохранённой в БД роли ИЛИ почте из
    /// конфигурации. Две проверки, а не одна, ради устойчивости к порядку событий:
    /// роль в БД проставляется только при вызове EnsureUser (GET /users/me), и до
    /// первого такого вызова строка суперадмина ещё содержит Role = User. Одной
    /// проверки по БД хватило бы, чтобы запереть админку сразу после развёртывания.
    ///
    /// Регистрируется как Scoped: обращается к репозиторию, который сам Scoped
    /// (обработчики авторизации по умолчанию считаются Singleton — это привело бы
    /// к captive dependency).
    /// </summary>
    public sealed class SuperAdminHandler : AuthorizationHandler<SuperAdminRequirement>
    {
        private readonly IUserRepository _users;
        private readonly SuperAdminPolicy _policy;

        public SuperAdminHandler(IUserRepository users, SuperAdminPolicy policy)
        {
            _users = users;
            _policy = policy;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SuperAdminRequirement requirement)
        {
            var principal = context.User;
            if (principal?.Identity?.IsAuthenticated != true) return;

            if (_policy.IsSuperAdminEmail(ReadEmail(principal)))
            {
                context.Succeed(requirement);
                return;
            }

            var userId = ReadUserId(principal);
            if (string.IsNullOrEmpty(userId)) return;

            var user = await _users.GetByIdAsync(userId);
            if (user?.Role == UserRole.SuperAdmin)
            {
                context.Succeed(requirement);
            }
        }

        // Тот же порядок, что в ApiControllerBase.GetCurrentUserId и
        // CustomUserIdProvider — идентификатор пользователя должен совпадать везде.
        private static string? ReadUserId(ClaimsPrincipal principal) =>
            principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private static string? ReadEmail(ClaimsPrincipal principal) =>
            principal.FindFirst("preferred_username")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value
            ?? principal.FindFirst("emails")?.Value;
    }
}
