using System.Threading;
using System.Threading.Tasks;

namespace Application.Common
{
    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    public sealed record EmailMessage(string To, string Subject, string PlainText, string Html)
    {
        /// <summary>
        /// Тип записи в outbox: письма уходят не из запроса пользователя, а фоновым
        /// OutboxDispatcher — так недоступность почтового сервиса не тормозит и не
        /// роняет запрос, а письмо будет переотправлено с backoff.
        /// </summary>
        public const string OutboxType = "Email";
    }
}
