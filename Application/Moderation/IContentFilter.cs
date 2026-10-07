using System.Collections.Generic;

namespace Application.Moderation
{
    public interface IContentFilter
    {
        ContentCheckResult Check(string? text);
    }

    /// <param name="MatchedWords">Найденные слова в том виде, как их написал пользователь.</param>
    /// <param name="Suggestion">
    /// Тот же текст с заменой плохих слов на приличные ("пиздец" → "капец"), чтобы
    /// клиент мог предложить отправить его вместо исходного. null, если хорошей
    /// замены нет.
    /// </param>
    public sealed record ContentCheckResult(bool IsClean, IReadOnlyList<string> MatchedWords, string? Suggestion)
    {
        public static readonly ContentCheckResult Clean = new(true, System.Array.Empty<string>(), null);
    }
}
