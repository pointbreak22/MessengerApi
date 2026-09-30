using MediatR;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Commands
{
    // Возвращает существующего пользователя по Id либо создаёт его (первый вход через CIAM).
    //
    // IsSuperAdmin решает вызывающая сторона (WebAPI), а не обработчик: признак
    // выводится из конфигурации приложения ("Admin:SuperAdminEmail"), а
    // Application-слой о конфигурации знать не должен.
    public record EnsureUserCommand(string Id, string UserName, string? Email = null, bool IsSuperAdmin = false) : IRequest<UserDto>;
}
