using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace Application.Moderation
{
    public sealed record ModerationOutcome(int StrikeNumber, int MaxStrikes, bool Banned);

    public interface IModerationService
    {
        /// <summary>
        /// Пропускает действие, если все тексты чистые. Иначе засчитывает нарушение
        /// (предупреждение на почту, на MaxStrikes-е — бан) и бросает
        /// ContentViolationException — действие не выполняется.
        /// </summary>
        Task EnsureCleanAsync(string actorId, IEnumerable<ModeratedText> texts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Засчитывает нарушение уже загруженному пользователю, ничего не бросая.
        /// Для случаев, когда действие нельзя просто отклонить — например, ник из
        /// токена при входе: его заменяют, а вход не блокируют.
        /// </summary>
        Task<ModerationOutcome> PenalizeAsync(User user, ModerationTarget target, string action, string content, ContentCheckResult check, CancellationToken cancellationToken = default);
    }

    public sealed class ModerationService : IModerationService
    {
        private readonly IContentFilter _filter;
        private readonly IUserRepository _users;
        private readonly IModerationRepository _violations;
        private readonly IIpBanRepository _ipBans;
        private readonly IOutboxRepository _outbox;
        private readonly IOutboxSignal _outboxSignal;
        private readonly IBanStatusCache _banCache;
        private readonly IIpBanCache _ipBanCache;
        private readonly IRealtimeNotifier _notifier;
        private readonly IClientContext _client;
        private readonly ModerationSettings _settings;

        public ModerationService(
            IContentFilter filter,
            IUserRepository users,
            IModerationRepository violations,
            IIpBanRepository ipBans,
            IOutboxRepository outbox,
            IOutboxSignal outboxSignal,
            IBanStatusCache banCache,
            IIpBanCache ipBanCache,
            IRealtimeNotifier notifier,
            IClientContext client,
            ModerationSettings settings)
        {
            _filter = filter;
            _users = users;
            _violations = violations;
            _ipBans = ipBans;
            _outbox = outbox;
            _outboxSignal = outboxSignal;
            _banCache = banCache;
            _ipBanCache = ipBanCache;
            _notifier = notifier;
            _client = client;
            _settings = settings;
        }

        public async Task EnsureCleanAsync(string actorId, IEnumerable<ModeratedText> texts, CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled) return;

            foreach (var item in texts)
            {
                var check = _filter.Check(item.Text);
                if (check.IsClean) continue;

                var user = await _users.GetByIdAsync(actorId);
                var outcome = user != null
                    ? await PenalizeAsync(user, item.Target, item.Action, item.Text!, check, cancellationToken)
                    : new ModerationOutcome(0, MaxStrikes, false);

                throw new ContentViolationException(item.Target, check.MatchedWords, check.Suggestion, outcome.StrikeNumber, outcome.MaxStrikes, outcome.Banned);
            }
        }

        public async Task<ModerationOutcome> PenalizeAsync(User user, ModerationTarget target, string action, string content, ContentCheckResult check, CancellationToken cancellationToken = default)
        {
            // Уже забаненный сюда почти не доходит (middleware/фильтр хаба), а если
            // дошёл — наказывать дальше некуда.
            if (user.IsBanned) return new ModerationOutcome(0, MaxStrikes, true);

            var now = DateTime.UtcNow;
            var ip = _client.IpAddress;

            // Суперадмина не баним (это же правило в AdminController.BanUser) —
            // текст всё равно не пропускаем, в журнал и в Telegram пишем, но без
            // предупреждений, писем и учёта для автобана IP.
            var exempt = user.Role == UserRole.SuperAdmin;
            var strike = 0;
            var banned = false;
            if (!exempt)
            {
                strike = user.AddModerationStrike(now, Decay);
                banned = strike >= MaxStrikes;
                if (banned) user.Ban();
            }

            var violation = ModerationViolation.Create(user.Id, target, content, string.Join(", ", check.MatchedWords), strike, banned, ip);

            var notifications = new List<OutboxMessage>();
            if (!exempt && !string.IsNullOrWhiteSpace(user.Email))
            {
                var message = ModerationEmails.Build(user.Email!, user.UserName, target, check.MatchedWords, strike, MaxStrikes, DecayDays, banned);
                notifications.Add(OutboxMessage.Create(EmailMessage.OutboxType, JsonSerializer.Serialize(message)));
            }

            await _violations.RecordViolationAsync(user, violation, notifications);

            if (banned)
            {
                _banCache.Invalidate(user.Id);
                // Как при бане из админки: клиент сразу показывает экран блокировки.
                try { await _notifier.NotifyUserAsync(user.Id, "AccountBanned", new { reason = "moderation" }); }
                catch { }
            }

            // Считается уже после сохранения, чтобы текущее нарушение тоже вошло в счёт.
            var ipReport = exempt ? null : await CheckIpAsync(ip, now);

            // Уведомление в Telegram — отдельно от основного сохранения: если оно
            // потеряется, страйк и бан всё равно должны остаться.
            var alert = ModerationAlerts.Build(user, action, content, check.MatchedWords, strike, MaxStrikes, banned, exempt, ip, ipReport);
            await _outbox.AddAsync(OutboxMessage.Create(TelegramAlert.OutboxType, JsonSerializer.Serialize(alert)));
            _outboxSignal.Notify();

            return new ModerationOutcome(strike, MaxStrikes, banned);
        }

        // Автобан IP: если с этого адреса за последние StrikeDecayDays дней страйки
        // получили IpBanUserThreshold разных аккаунтов — блокируем сам адрес, чтобы
        // нельзя было бесконечно заводить новые аккаунты вместо забаненных.
        private async Task<IpReport?> CheckIpAsync(string? ip, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(ip)) return null;

            var offenders = await _violations.CountOffendersByIpAsync(ip, now - Decay);
            var threshold = _settings.IpBanUserThreshold;
            if (threshold <= 0 || offenders < threshold) return new IpReport(offenders, false, null);
            if (await _ipBans.IsBannedAsync(ip, now)) return new IpReport(offenders, false, null);

            DateTime? expires = _settings.IpBanDays > 0 ? now.AddDays(_settings.IpBanDays) : null;
            var ban = IpBan.Create(ip, $"Автобан: разных аккаунтов с нарушениями с этого IP — {offenders}", isAutomatic: true, expires);
            await _ipBans.AddAsync(ban);
            _ipBanCache.InvalidateIp(ip);
            return new IpReport(offenders, true, expires);
        }

        private int MaxStrikes => Math.Max(1, _settings.MaxStrikes);
        private int DecayDays => Math.Max(1, _settings.StrikeDecayDays);
        private TimeSpan Decay => TimeSpan.FromDays(DecayDays);
    }

    /// <param name="OffendersFromIp">Сколько разных аккаунтов нарушали с этого IP за период.</param>
    /// <param name="IpBannedNow">Этим нарушением IP был автоматически заблокирован.</param>
    internal sealed record IpReport(int OffendersFromIp, bool IpBannedNow, DateTime? BannedUntil);
}
