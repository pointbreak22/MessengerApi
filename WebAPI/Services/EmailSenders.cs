using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Azure;
using Azure.Communication.Email;
using EmailMessage = Application.Common.EmailMessage;
using Microsoft.Extensions.Logging;

namespace WebAPI.Services
{
    /// <summary>
    /// Письма через Azure Communication Services Email. Настройки:
    /// "Email:ConnectionString" (секрет — Key Vault / appsettings.Secrets.json) и
    /// "Email:SenderAddress" (например DoNotReply@xxxx.azurecomm.net).
    /// Ошибка отправки пробрасывается — OutboxDispatcher повторит с backoff.
    /// </summary>
    public sealed class AzureCommunicationEmailSender : IEmailSender
    {
        private readonly EmailClient _client;
        private readonly string _sender;

        public AzureCommunicationEmailSender(EmailClient client, string sender)
        {
            _client = client;
            _sender = sender;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var content = new EmailContent(message.Subject) { PlainText = message.PlainText, Html = message.Html };
            var email = new Azure.Communication.Email.EmailMessage(_sender, message.To, content);
            // WaitUntil.Started: ждать финального статуса доставки незачем — сервис
            // принял письмо, дальше это его ретраи.
            await _client.SendAsync(WaitUntil.Started, email, cancellationToken);
        }
    }

    /// <summary>
    /// Почта не настроена (локальная разработка): письмо только пишется в лог,
    /// чтобы модерация работала и без Azure Communication Services.
    /// </summary>
    public sealed class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Email не настроен (Email:ConnectionString), письмо не отправлено. To={To}, Subject={Subject}", message.To, message.Subject);
            return Task.CompletedTask;
        }
    }
}
