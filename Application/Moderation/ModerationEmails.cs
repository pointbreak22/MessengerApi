using System.Collections.Generic;
using System.Linq;
using System.Net;
using Application.Common;
using Domain.Enums;

namespace Application.Moderation
{
    /// <summary>Тексты писем автомодерации: предупреждение и уведомление о блокировке.</summary>
    internal static class ModerationEmails
    {
        public static EmailMessage Build(string to, string userName, ModerationTarget target, IReadOnlyList<string> matchedWords, int strike, int maxStrikes, int decayDays, bool banned)
        {
            var where = target switch
            {
                ModerationTarget.UserName => "в вашем имени пользователя",
                ModerationTarget.ChatName => "в названии чата",
                _ => "в вашем сообщении",
            };
            var words = string.Join(", ", matchedWords.Distinct().Select(Mask));

            string subject;
            string body;
            if (banned)
            {
                subject = "Ваш аккаунт в мессенджере заблокирован";
                body = $"Мы снова обнаружили нецензурную или непристойную лексику {where} ({words}). " +
                       $"Это {strike}-е нарушение правил общения, поэтому аккаунт заблокирован. " +
                       "Если вы считаете, что это ошибка, ответьте на это письмо.";
            }
            else
            {
                var left = maxStrikes - strike;
                subject = $"Предупреждение {strike} из {maxStrikes - 1}: нарушение правил общения";
                body = $"Мы обнаружили нецензурную или непристойную лексику {where} ({words}), поэтому это действие было отклонено. " +
                       (left == 1
                           ? "Это последнее предупреждение: при следующем нарушении аккаунт будет заблокирован."
                           : $"На {maxStrikes}-м нарушении аккаунт будет заблокирован.") +
                       $" Предупреждения сгорают, если не нарушать правила в течение {decayDays} дней.";
            }

            var greeting = $"Здравствуйте, {userName}!";
            var plain = $"{greeting}\n\n{body}\n\n— Команда мессенджера";
            var html = $"<p>{WebUtility.HtmlEncode(greeting)}</p><p>{WebUtility.HtmlEncode(body)}</p><p>— Команда мессенджера</p>";
            return new EmailMessage(to, subject, plain, html);
        }

        // В письме не повторяем мат целиком: "пиздец" → "п****ц".
        private static string Mask(string word)
        {
            if (word.Length <= 2) return new string('*', word.Length);
            return word[0] + new string('*', word.Length - 2) + word[^1];
        }
    }
}
