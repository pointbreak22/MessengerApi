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
        private readonly IOutboxSignal _outboxSignal;
        private readonly IBanStatusCache _banCache;
        private readonly IRealtimeNotifier _notifier;
        private readonly ModerationSettings _settings;

        public ModerationService(
            IContentFilter filter,
            IUserRepository users,
            IModerationRepository violations,
            IOutboxSignal outboxSignal,
            IBanStatusCache banCache,
            IRealtimeNotifier notifier,
            ModerationSettings settings)
        {
            _filter = filter;
            _users = users;
            _violations = violations;
            _outboxSignal = outboxSignal;
            _banCache = banCache;
            _notifier = notifier;
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

            // Суперадмина не баним (это же правило в AdminController.BanUser) —
            // текст всё равно не пропускаем, в журнал и в Telegram пишем, но без
            // предупреждений и писем.
            var exempt = user.Role == UserRole.SuperAdmin;
            var strike = 0;
            var banned = false;
            if (!exempt)
            {
                strike = user.AddModerationStrike(DateTime.UtcNow, TimeSpan.FromDays(DecayDays));
                banned = strike >= MaxStrikes;
                if (banned) user.Ban();
            }

            var violation = ModerationViolation.Create(user.Id, target, content, string.Join(", ", check.MatchedWords), strike, banned);

            var notifications = new List<OutboxMessage>();
            if (!exempt && !string.IsNullOrWhiteSpace(user.Email))
            {
                var message = ModerationEmails.Build(user.Email!, user.UserName, target, check.MatchedWords, strike, MaxStrikes, DecayDays, banned);
                notifications.Add(OutboxMessage.Create(EmailMessage.OutboxType, JsonSerializer.Serialize(message)));
            }
            var alert = ModerationAlerts.Build(user, action, content, check.MatchedWords, strike, MaxStrikes, banned, exempt);
            notifications.Add(OutboxMessage.Create(TelegramAlert.OutboxType, JsonSerializer.Serialize(alert)));

            await _violations.RecordViolationAsync(user, violation, notifications);
            _outboxSignal.Notify();

            if (banned)
            {
                _banCache.Invalidate(user.Id);
                // Как при бане из админки: клиент сразу показывает экран блокировки.
                try { await _notifier.NotifyUserAsync(user.Id, "AccountBanned", new { reason = "moderation" }); }
                catch { }
            }

            return new ModerationOutcome(strike, MaxStrikes, banned);
        }

        private int MaxStrikes => Math.Max(1, _settings.MaxStrikes);
        private int DecayDays => Math.Max(1, _settings.StrikeDecayDays);
    }
}
