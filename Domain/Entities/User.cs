using System;
using System.Collections.Generic;
using System.Text;
using Domain.Enums;

namespace Domain.Entities
{
    public class User
    {
        public string Id { get; private set; } // Строка, так как OAuth2 (Google/Azure) обычно выдает строковый ID (Sub)
        public string UserName { get; private set; }
        public string? AvatarUrl { get; private set; }
        public bool IsOnline { get; private set; }
        public DateTime LastSeenAt { get; private set; }

        // Логин, под которым пользователь входит через CIAM. Хранится отдельно от
        // UserName, потому что UserName редактируемое (в том числе админом), а email
        // идентифицирует человека и показывается в админке. Nullable: набор claims в
        // токене зависит от user flow, email может и не прийти.
        public string? Email { get; private set; }

        public UserRole Role { get; private set; }

        // Бан от суперадмина: такой пользователь не может ничего делать в API и
        // хабе и не показывается другим (поиск, друзья, чаты, сообщения).
        public bool IsBanned { get; private set; }
        public DateTime? BannedAt { get; private set; }

        // Предупреждения автомодерации (мат в нике/сообщениях). Сгорают, если
        // пользователь не нарушал дольше, чем decay (см. AddModerationStrike).
        public int ModerationStrikes { get; private set; }
        public DateTime? LastStrikeAt { get; private set; }

        private User() { }

        public static User Create(string id, string userName, string? avatarUrl, string? email = null, UserRole role = UserRole.User)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Имя не может быть пустым.");
            return new User
            {
                Id = id,
                UserName = userName,
                AvatarUrl = avatarUrl,
                Email = email,
                Role = role,
                IsOnline = false,
                LastSeenAt = DateTime.UtcNow
            };
        }

        public void SetRole(UserRole role)
        {
            Role = role;
        }

        // Вызывается при каждом входе: email в токене может появиться позже, чем
        // была создана запись пользователя.
        public void SetEmail(string? email)
        {
            if (!string.IsNullOrWhiteSpace(email)) Email = email.Trim();
        }

        public void Ban()
        {
            if (IsBanned) return;
            IsBanned = true;
            BannedAt = DateTime.UtcNow;
            IsOnline = false;
        }

        // Разбаненный начинает с чистого листа — иначе первое же нарушение
        // после разбана снова дало бы бан.
        public void Unban()
        {
            IsBanned = false;
            BannedAt = null;
            ModerationStrikes = 0;
            LastStrikeAt = null;
        }

        /// <summary>
        /// Засчитывает нарушение и возвращает номер текущего предупреждения. Если с
        /// прошлого нарушения прошло больше decay, старые предупреждения сгорают и
        /// счёт начинается заново.
        /// </summary>
        public int AddModerationStrike(DateTime now, TimeSpan decay)
        {
            if (LastStrikeAt.HasValue && now - LastStrikeAt.Value > decay) ModerationStrikes = 0;
            ModerationStrikes++;
            LastStrikeAt = now;
            return ModerationStrikes;
        }

        public void UpdateStatus(bool isOnline)
        {
            IsOnline = isOnline;
            LastSeenAt = DateTime.UtcNow;
        }

        public void UpdateAvatar(string? avatarUrl)
        {
            AvatarUrl = avatarUrl;
        }

        public void UpdateProfile(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Имя не может быть пустым.");
            UserName = userName.Trim();
        }
    }
}
