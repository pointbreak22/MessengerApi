namespace Application.Common
{
    /// <summary>
    /// Сообщение администратору в Telegram (тот же бот и получатель, что у PointCraft).
    /// Как и письма, уходит через outbox: недоступность Telegram не тормозит запрос,
    /// а неудачная отправка повторяется с backoff.
    /// </summary>
    public sealed record TelegramAlert(string Text)
    {
        public const string OutboxType = "TelegramAlert";
    }

    public interface ITelegramSender
    {
        Task SendAsync(TelegramAlert alert, CancellationToken cancellationToken = default);
    }
}
