using System;
using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Запись о нарушении, пойманном автомодерацией: что именно написал пользователь,
    /// какое это было предупреждение по счёту и закончилось ли баном. Нужна админке,
    /// чтобы было видно, за что человек заблокирован, и чтобы разбирать ложные срабатывания.
    /// </summary>
    public class ModerationViolation
    {
        public const int MaxContentLength = 2000;
        public const int MaxMatchedWordsLength = 500;

        public Guid Id { get; private set; }
        public string UserId { get; private set; } = null!;
        public ModerationTarget Target { get; private set; }
        public string Content { get; private set; } = null!;
        public string MatchedWords { get; private set; } = null!;
        public int StrikeNumber { get; private set; }
        public bool ResultedInBan { get; private set; }
        // С какого IP было нарушение — по нему считается автобан IP (см. IpBan).
        public string? IpAddress { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private ModerationViolation() { }

        public static ModerationViolation Create(string userId, ModerationTarget target, string content, string matchedWords, int strikeNumber, bool resultedInBan, string? ipAddress = null)
        {
            return new ModerationViolation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Target = target,
                Content = content.Length > MaxContentLength ? content.Substring(0, MaxContentLength) : content,
                MatchedWords = matchedWords.Length > MaxMatchedWordsLength ? matchedWords.Substring(0, MaxMatchedWordsLength) : matchedWords,
                StrikeNumber = strikeNumber,
                ResultedInBan = resultedInBan,
                IpAddress = ipAddress != null && ipAddress.Length > IpBan.MaxIpLength ? ipAddress.Substring(0, IpBan.MaxIpLength) : ipAddress,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
