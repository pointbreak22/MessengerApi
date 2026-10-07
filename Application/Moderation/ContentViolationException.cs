using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Application.Moderation
{
    /// <summary>
    /// Действие отклонено автомодерацией. WebAPI превращает его в ответ 422
    /// (REST) или событие ContentViolation + HubException (SignalR).
    /// </summary>
    public sealed class ContentViolationException : Exception
    {
        public const string ErrorCode = "content_violation";

        public ModerationTarget Target { get; }
        public IReadOnlyList<string> MatchedWords { get; }
        public string? Suggestion { get; }
        public int StrikeNumber { get; }
        public int MaxStrikes { get; }
        public bool Banned { get; }

        public ContentViolationException(ModerationTarget target, IReadOnlyList<string> matchedWords, string? suggestion, int strikeNumber, int maxStrikes, bool banned)
            : base(BuildMessage(target, strikeNumber, maxStrikes, banned))
        {
            Target = target;
            MatchedWords = matchedWords;
            Suggestion = suggestion;
            StrikeNumber = strikeNumber;
            MaxStrikes = maxStrikes;
            Banned = banned;
        }

        private static string BuildMessage(ModerationTarget target, int strike, int max, bool banned)
        {
            var what = target switch
            {
                ModerationTarget.UserName => "Имя содержит недопустимые слова.",
                ModerationTarget.ChatName => "Название чата содержит недопустимые слова.",
                _ => "Сообщение содержит недопустимые слова и не отправлено.",
            };

            if (banned) return $"{what} Это {strike}-е нарушение — аккаунт заблокирован.";
            // strike == 0: суперадмина не наказываем, только не пропускаем текст.
            if (strike == 0) return what;
            return $"{what} Предупреждение {strike} из {max - 1}: на {max}-е нарушение аккаунт будет заблокирован.";
        }
    }
}
