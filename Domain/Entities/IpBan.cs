using System;

namespace Domain.Entities
{
    /// <summary>
    /// Блокировка IP-адреса: с него не принимается ни один запрос к API и хабу
    /// (кроме суперадмина). Ставится вручную из админки или автоматически, когда
    /// с одного IP нарушают правила несколько разных аккаунтов.
    ///
    /// Один IP бывает общим для многих людей (мобильный оператор, офис), поэтому
    /// автобан по умолчанию временный — ExpiresAt.
    /// </summary>
    public class IpBan
    {
        public const int MaxIpLength = 64;
        public const int MaxReasonLength = 500;

        public Guid Id { get; private set; }
        public string IpAddress { get; private set; } = null!;
        public string Reason { get; private set; } = null!;
        public bool IsAutomatic { get; private set; }
        public DateTime CreatedAt { get; private set; }

        /// <summary>null — бессрочно.</summary>
        public DateTime? ExpiresAt { get; private set; }

        private IpBan() { }

        public static IpBan Create(string ipAddress, string reason, bool isAutomatic, DateTime? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) throw new ArgumentException("IP не может быть пустым.");
            ipAddress = ipAddress.Trim();
            reason = string.IsNullOrWhiteSpace(reason) ? "—" : reason.Trim();
            return new IpBan
            {
                Id = Guid.NewGuid(),
                IpAddress = ipAddress.Length > MaxIpLength ? ipAddress.Substring(0, MaxIpLength) : ipAddress,
                Reason = reason.Length > MaxReasonLength ? reason.Substring(0, MaxReasonLength) : reason,
                IsAutomatic = isAutomatic,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };
        }

        public bool IsActive(DateTime now) => ExpiresAt == null || ExpiresAt > now;
    }
}
