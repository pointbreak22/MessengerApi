using System;
using System.Threading.Tasks;
using Application.Common;
using Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace WebAPI.Services
{
    /// <summary>
    /// "Заблокирован ли этот IP" — для каждого запроса и вызова хаба, по образцу
    /// BanStatusService: короткий кэш в памяти, сброс сразу при бане/разбане.
    /// Singleton, репозиторий (Scoped) берётся из отдельного scope.
    /// </summary>
    public sealed class IpBanStatusService : IIpBanCache
    {
        public const string BannedErrorCode = "ip_banned";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;

        public IpBanStatusService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> IsBannedAsync(string? ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return false;

            if (_cache.TryGetValue(Key(ipAddress), out bool cached)) return cached;

            using var scope = _scopeFactory.CreateScope();
            var bans = scope.ServiceProvider.GetRequiredService<IIpBanRepository>();
            var banned = await bans.IsBannedAsync(ipAddress, DateTime.UtcNow);

            _cache.Set(Key(ipAddress), banned, CacheTtl);
            return banned;
        }

        public void InvalidateIp(string ipAddress) => _cache.Remove(Key(ipAddress));

        private static string Key(string ip) => $"ipban:{ip}";
    }
}
