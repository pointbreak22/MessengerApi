using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebAPI.Services
{
    /// <summary>
    /// Уведомления админу через Telegram Bot API — тот же бот, что у PointCraft
    /// (там же описано, как получить токен и chat id: PointCraftAWS/DEPLOY.md, раздел 8).
    /// Настройки "TelegramBot": Enabled, BotToken (секрет — appsettings.Secrets.json /
    /// Key Vault TelegramBot--BotToken), ChatIds (массив получателей).
    ///
    /// Не настроен — молча пропускает. Ошибка Telegram пробрасывается, чтобы
    /// OutboxDispatcher повторил отправку с backoff.
    /// </summary>
    public sealed class TelegramSender : ITelegramSender
    {
        // Telegram отклоняет сообщение длиннее 4096 символов целиком.
        private const int MessageLimit = 4096;

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramSender> _logger;

        public TelegramSender(HttpClient http, IConfiguration configuration, ILogger<TelegramSender> logger)
        {
            _http = http;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAsync(TelegramAlert alert, CancellationToken cancellationToken = default)
        {
            var token = _configuration["TelegramBot:BotToken"];
            var chatIds = _configuration.GetSection("TelegramBot:ChatIds").Get<string[]>() ?? Array.Empty<string>();
            if (!_configuration.GetValue<bool>("TelegramBot:Enabled") || string.IsNullOrWhiteSpace(token) || chatIds.Length == 0)
            {
                _logger.LogInformation("Telegram не настроен (TelegramBot), уведомление пропущено.");
                return;
            }

            var text = alert.Text.Length > MessageLimit ? alert.Text[..(MessageLimit - 15)] + "\n…(обрезано)" : alert.Text;

            foreach (var chatId in chatIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                var response = await _http.PostAsJsonAsync($"/bot{token}/sendMessage", new { chat_id = chatId, text }, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Токен в URL — в текст ошибки его не тащим.
                    throw new HttpRequestException($"Telegram sendMessage to {chatId} failed: {(int)response.StatusCode}");
                }
            }
        }
    }
}
