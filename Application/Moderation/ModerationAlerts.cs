using System.Collections.Generic;
using System.Linq;
using Application.Common;
using Domain.Entities;

namespace Application.Moderation
{
    /// <summary>Текст уведомления в Telegram о нарушении: кто, что пытался сделать, чем кончилось.</summary>
    internal static class ModerationAlerts
    {
        private const int MaxQuoteLength = 500;

        public static TelegramAlert Build(User user, string action, string content, IReadOnlyList<string> matchedWords, int strike, int maxStrikes, bool banned, bool exempt)
        {
            var result = exempt
                ? "без страйка (суперадмин)"
                : banned
                    ? $"⛔ {strike}-е нарушение — аккаунт ЗАБЛОКИРОВАН"
                    : $"предупреждение {strike} из {maxStrikes - 1} (бан на {maxStrikes}-м)";

            var quote = content.Length > MaxQuoteLength ? content[..MaxQuoteLength] + "…" : content;

            var text = (banned ? "⛔" : "⚠️") + " Мессенджер: нарушение автомодерации\n\n" +
                       $"Пользователь: {user.UserName}\n" +
                       $"Почта: {(string.IsNullOrWhiteSpace(user.Email) ? "не указана" : user.Email)}\n" +
                       $"Id: {user.Id}\n" +
                       $"Действие: {action}\n" +
                       $"Найдено: {string.Join(", ", matchedWords.Distinct())}\n" +
                       $"Итог: {result}\n\n" +
                       $"Текст:\n{quote}";

            return new TelegramAlert(text);
        }
    }
}
