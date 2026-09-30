using System;
using Microsoft.Extensions.Configuration;

namespace WebAPI.Services
{
    /// <summary>
    /// Единственный источник правды о том, кто является суперадмином: почта из
    /// конфигурации ("Admin:SuperAdminEmail"). Держится отдельно от базы, чтобы
    /// назначение прав не требовало ручных правок в БД — роль в таблице лишь
    /// отражает это значение и проставляется при входе (EnsureUserCommand).
    ///
    /// Не задано в конфигурации — суперадмина нет вообще. Это намеренно: пустая
    /// настройка не должна случайно раздать права кому попало.
    /// </summary>
    public sealed class SuperAdminPolicy
    {
        private readonly string? _superAdminEmail;

        public SuperAdminPolicy(IConfiguration configuration)
        {
            _superAdminEmail = configuration["Admin:SuperAdminEmail"];
        }

        public bool IsSuperAdminEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(_superAdminEmail) || string.IsNullOrWhiteSpace(email)) return false;
            return string.Equals(email.Trim(), _superAdminEmail.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
