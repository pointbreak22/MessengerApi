using System;
using System.Threading.Tasks;
using Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace WebAPI.Services
{
    /// <summary>
    /// Отвечает на вопрос "забанен ли пользователь" для каждого запроса к API и
    /// каждого вызова метода хаба. Результат кэшируется в памяти на короткое время,
    /// чтобы не ходить в БД на каждый запрос; при бане/разбане через админку запись
    /// сбрасывается сразу. На нескольких инстансах остальные подхватят изменение не
    /// позже, чем через CacheTtl.
    ///
    /// Singleton, поэтому репозиторий (Scoped) берётся из отдельного scope.
    /// </summary>
    public sealed class BanStatusService
    {
        public const string BannedErrorCode = "account_banned";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public BanStatusService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> IsBannedAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            if (_cache.TryGetValue(Key(userId), out bool cached)) return cached;

            using var scope = _scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var banned = await users.IsBannedAsync(userId);

            _cache.Set(Key(userId), banned, CacheTtl);
            return banned;
        }

        public void Invalidate(string userId) => _cache.Remove(Key(userId));

        private static string Key(string userId) => $"ban:{userId}";
    }
}
