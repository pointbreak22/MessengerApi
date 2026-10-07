using System.Collections.Generic;
using Domain.Enums;

namespace Application.Moderation
{
    /// <summary>
    /// Команда, тексты которой проверяет автомодерация до выполнения обработчика
    /// (см. ContentModerationBehavior). Чтобы закрыть новую команду — достаточно
    /// реализовать этот интерфейс, в сам обработчик ничего добавлять не нужно.
    /// </summary>
    public interface IModeratedRequest
    {
        /// <summary>Кто выполняет действие — ему и засчитывается нарушение.</summary>
        string ModerationActorId { get; }

        IEnumerable<ModeratedText> GetModeratedTexts();
    }

    /// <param name="Action">Что пользователь пытался сделать — для уведомления админу ("отправка сообщения").</param>
    public readonly record struct ModeratedText(ModerationTarget Target, string? Text, string Action);
}
