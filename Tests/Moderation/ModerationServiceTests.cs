using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.Moderation;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Moq;
using Xunit;

namespace Tests.Moderation
{
    public class ModerationServiceTests
    {
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IModerationRepository> _violations = new();
        private readonly Mock<IBanStatusCache> _banCache = new();
        private readonly Mock<IRealtimeNotifier> _notifier = new();
        private readonly User _user = User.Create("u1", "Вася", null, "vasya@example.com");

        private ModerationService CreateService(int maxStrikes = 3)
        {
            _users.Setup(u => u.GetByIdAsync("u1")).ReturnsAsync(_user);
            return new ModerationService(
                new ProfanityFilter(), _users.Object, _violations.Object, Mock.Of<IOutboxSignal>(),
                _banCache.Object, _notifier.Object, new ModerationSettings { MaxStrikes = maxStrikes, StrikeDecayDays = 30 });
        }

        private static Task Send(ModerationService s, string text) =>
            s.EnsureCleanAsync("u1", new[] { new ModeratedText(ModerationTarget.Message, text, "отправка сообщения") });

        [Fact]
        public async Task Clean_text_passes_without_strike()
        {
            var service = CreateService();
            await Send(service, "привет, подстрахуй меня завтра");
            Assert.Equal(0, _user.ModerationStrikes);
            _violations.Verify(v => v.RecordViolationAsync(It.IsAny<User>(), It.IsAny<ModerationViolation>(), It.IsAny<IEnumerable<OutboxMessage>>()), Times.Never);
        }

        [Fact]
        public async Task Two_warnings_by_email_then_ban_on_third()
        {
            var service = CreateService();

            var first = await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "пиздец"));
            Assert.Equal(1, first.StrikeNumber);
            Assert.False(first.Banned);
            Assert.Equal("капец", first.Suggestion);

            var second = await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "х*й"));
            Assert.Equal(2, second.StrikeNumber);
            Assert.False(_user.IsBanned);

            var third = await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "бл@ть"));
            Assert.Equal(3, third.StrikeNumber);
            Assert.True(third.Banned);
            Assert.True(_user.IsBanned);

            // Каждое нарушение — запись в журнал, письмо пользователю и уведомление админу в Telegram.
            _violations.Verify(v => v.RecordViolationAsync(_user, It.IsAny<ModerationViolation>(),
                It.Is<IEnumerable<OutboxMessage>>(n =>
                    n.Any(o => o.Type == EmailMessage.OutboxType && o.Payload.Contains("vasya@example.com")) &&
                    n.Any(o => o.Type == TelegramAlert.OutboxType))), Times.Exactly(3));
            _banCache.Verify(c => c.Invalidate("u1"), Times.Once);
            _notifier.Verify(n => n.NotifyUserAsync("u1", "AccountBanned", It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void Strikes_expire_after_decay_period()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.Equal(1, _user.AddModerationStrike(start, TimeSpan.FromDays(30)));
            Assert.Equal(2, _user.AddModerationStrike(start.AddDays(10), TimeSpan.FromDays(30)));
            // больше 30 дней с последнего нарушения — счёт заново
            Assert.Equal(1, _user.AddModerationStrike(start.AddDays(41), TimeSpan.FromDays(30)));
        }

        [Fact]
        public async Task Super_admin_is_blocked_but_never_penalized()
        {
            _user.SetRole(UserRole.SuperAdmin);
            var service = CreateService(maxStrikes: 1);

            var ex = await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "сука"));
            Assert.Equal(0, ex.StrikeNumber);
            Assert.False(_user.IsBanned);
        }

        [Fact]
        public async Task Telegram_alert_names_user_email_action_and_outcome()
        {
            var sent = new List<OutboxMessage>();
            _violations
                .Setup(v => v.RecordViolationAsync(It.IsAny<User>(), It.IsAny<ModerationViolation>(), It.IsAny<IEnumerable<OutboxMessage>>()))
                .Callback<User, ModerationViolation, IEnumerable<OutboxMessage>>((_, _, n) => sent.AddRange(n))
                .Returns(Task.CompletedTask);
            var service = CreateService();

            await Assert.ThrowsAsync<ContentViolationException>(() =>
                service.EnsureCleanAsync("u1", new[] { new ModeratedText(ModerationTarget.UserName, "Хуеплёт", "смена ника") }));

            var alert = System.Text.Json.JsonSerializer.Deserialize<TelegramAlert>(sent.Single(o => o.Type == TelegramAlert.OutboxType).Payload)!;
            Assert.Contains("Вася", alert.Text);
            Assert.Contains("vasya@example.com", alert.Text);
            Assert.Contains("смена ника", alert.Text);
            Assert.Contains("Хуеплёт", alert.Text);
            Assert.Contains("предупреждение 1 из 2", alert.Text);
        }

        [Fact]
        public void Unban_resets_strikes()
        {
            _user.AddModerationStrike(DateTime.UtcNow, TimeSpan.FromDays(30));
            _user.Ban();
            _user.Unban();
            Assert.Equal(0, _user.ModerationStrikes);
        }
    }
}
