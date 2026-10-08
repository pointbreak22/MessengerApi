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
        private readonly Mock<IIpBanRepository> _ipBans = new();
        private readonly Mock<IIpBanCache> _ipBanCache = new();
        private readonly Mock<IOutboxRepository> _outbox = new();
        private readonly Mock<IClientContext> _client = new();
        // Всё, что ушло в outbox помимо атомарного сохранения нарушения (Telegram).
        private readonly List<OutboxMessage> _queued = new();
        private readonly User _user = User.Create("u1", "Вася", null, "vasya@example.com");

        private ModerationService CreateService(int maxStrikes = 3, int ipThreshold = 3)
        {
            _users.Setup(u => u.GetByIdAsync("u1")).ReturnsAsync(_user);
            _client.Setup(c => c.IpAddress).Returns("203.0.113.7");
            _outbox.Setup(o => o.AddAsync(It.IsAny<OutboxMessage>())).Callback<OutboxMessage>(_queued.Add).Returns(Task.CompletedTask);
            return new ModerationService(
                new ProfanityFilter(), _users.Object, _violations.Object, _ipBans.Object, _outbox.Object, Mock.Of<IOutboxSignal>(),
                _banCache.Object, _ipBanCache.Object, _notifier.Object, _client.Object,
                new ModerationSettings { MaxStrikes = maxStrikes, StrikeDecayDays = 30, IpBanUserThreshold = ipThreshold, IpBanDays = 7 });
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

            // Каждое нарушение — запись в журнал (с IP) вместе с письмом пользователю, плюс уведомление админу в Telegram.
            _violations.Verify(v => v.RecordViolationAsync(_user, It.Is<ModerationViolation>(x => x.IpAddress == "203.0.113.7"),
                It.Is<IEnumerable<OutboxMessage>>(n =>
                    n.Any(o => o.Type == EmailMessage.OutboxType && o.Payload.Contains("vasya@example.com")))), Times.Exactly(3));
            Assert.Equal(3, _queued.Count(o => o.Type == TelegramAlert.OutboxType));
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
            var service = CreateService();

            await Assert.ThrowsAsync<ContentViolationException>(() =>
                service.EnsureCleanAsync("u1", new[] { new ModeratedText(ModerationTarget.UserName, "Хуеплёт", "смена ника") }));

            var alert = System.Text.Json.JsonSerializer.Deserialize<TelegramAlert>(_queued.Single(o => o.Type == TelegramAlert.OutboxType).Payload)!;
            Assert.Contains("Вася", alert.Text);
            Assert.Contains("vasya@example.com", alert.Text);
            Assert.Contains("смена ника", alert.Text);
            Assert.Contains("Хуеплёт", alert.Text);
            Assert.Contains("предупреждение 1 из 2", alert.Text);
            Assert.Contains("203.0.113.7", alert.Text);
        }

        [Fact]
        public async Task Ip_is_auto_banned_when_enough_accounts_offend_from_it()
        {
            // С этого IP (с учётом текущего) нарушали уже 3 разных аккаунта.
            _violations.Setup(v => v.CountOffendersByIpAsync("203.0.113.7", It.IsAny<DateTime>())).ReturnsAsync(3);
            IpBan? created = null;
            _ipBans.Setup(b => b.AddAsync(It.IsAny<IpBan>())).Callback<IpBan>(b => created = b).Returns(Task.CompletedTask);
            var service = CreateService(ipThreshold: 3);

            await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "сука"));

            Assert.NotNull(created);
            Assert.Equal("203.0.113.7", created!.IpAddress);
            Assert.True(created.IsAutomatic);
            Assert.NotNull(created.ExpiresAt);
            _ipBanCache.Verify(c => c.InvalidateIp("203.0.113.7"), Times.Once);
            var alert = System.Text.Json.JsonSerializer.Deserialize<TelegramAlert>(_queued.Single().Payload)!;
            Assert.Contains("ЗАБЛОКИРОВАН автоматически", alert.Text);
        }

        [Fact]
        public async Task Ip_is_not_banned_below_threshold_or_when_already_banned()
        {
            _violations.Setup(v => v.CountOffendersByIpAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(2);
            var service = CreateService(ipThreshold: 3);
            await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "сука"));

            _violations.Setup(v => v.CountOffendersByIpAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(5);
            _ipBans.Setup(b => b.IsBannedAsync("203.0.113.7", It.IsAny<DateTime>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<ContentViolationException>(() => Send(service, "сука"));

            _ipBans.Verify(b => b.AddAsync(It.IsAny<IpBan>()), Times.Never);
        }

        [Theory]
        [InlineData("198.51.100.1:51234", null, "198.51.100.1")]
        [InlineData("6.6.6.6, 198.51.100.1:51234", null, "198.51.100.1")]   // подделанное значение левее не берём
        [InlineData("[2001:db8::1]:443", null, "2001:db8::1")]
        [InlineData(null, "10.0.0.5", "10.0.0.5")]
        public void Client_ip_comes_from_last_forwarded_hop(string? forwarded, string? remote, string expected)
        {
            var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            if (forwarded != null) http.Request.Headers["X-Forwarded-For"] = forwarded;
            if (remote != null) http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remote);
            Assert.Equal(expected, WebAPI.Services.ClientIp.Resolve(http));
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
