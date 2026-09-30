using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Domain.Repositories;
using WebAPI.Hubs;

namespace WebAPI.Services
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<ChatHub> _hub;
        private readonly ILogger<OutboxDispatcher> _logger;
        private readonly OutboxSignal _signal;

        // Upper bound on how long a message can sit unnoticed when no wake-up
        // signal arrives — a row written by another instance, or a signal lost
        // to a race. Under normal operation the signal fires first and this
        // never elapses.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

        public OutboxDispatcher(IServiceScopeFactory scopeFactory, IHubContext<ChatHub> hub, ILogger<OutboxDispatcher> logger, OutboxSignal signal)
        {
            _scopeFactory = scopeFactory;
            _hub = hub;
            _logger = logger;
            _signal = signal;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxDispatcher started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    var chats = scope.ServiceProvider.GetRequiredService<IChatRepository>();

                    var pending = await outbox.GetPendingAsync(50);
                    foreach (var item in pending)
                    {
                        // десериализуем payload и отправляем через SignalR
                        try
                        {
                            var doc = System.Text.Json.JsonDocument.Parse(item.Payload);
                            if (doc.RootElement.TryGetProperty("ChatId", out var chatIdProp))
                            {
                                var chatGuid = chatIdProp.GetGuid();
                                var root = doc.RootElement;
                                var attachmentUrl = root.TryGetProperty("AttachmentUrl", out var a) && a.ValueKind != System.Text.Json.JsonValueKind.Null
                                    ? a.GetString()
                                    : null;
                                var replyToMessageId = root.TryGetProperty("ReplyToMessageId", out var r) && r.ValueKind != System.Text.Json.JsonValueKind.Null
                                    ? r.GetGuid()
                                    : (Guid?)null;

                                var payload = new
                                {
                                    messageId = root.GetProperty("MessageId").GetGuid(),
                                    chatId = chatGuid,
                                    senderId = root.GetProperty("SenderId").GetString(),
                                    text = root.TryGetProperty("Text", out var t) ? t.GetString() : null,
                                    attachmentUrl,
                                    replyToMessageId
                                };

                                // Group(chatId) only reaches connections that called JoinChatRoom for
                                // this chat — i.e. members who currently have it open. Members who
                                // have it in their chat list but aren't looking at it right now would
                                // never get NewMessage at all (no unread badge bump, no sound, nothing,
                                // until they happen to open the chat or reload the page). Push to every
                                // actual member by user id instead — reaches everyone regardless of
                                // which chat room they're sitting in right now.
                                var chat = await chats.GetByIdAsync(chatGuid);
                                var memberIds = chat?.Members.Select(m => m.UserId).ToList() ?? new List<string>();

                                if (memberIds.Count > 0)
                                {
                                    await _hub.Clients.Users(memberIds).SendAsync("NewMessage", payload, cancellationToken: stoppingToken);
                                }
                                else
                                {
                                    // Chat gone/members unresolvable for some reason — fall back to the
                                    // room broadcast rather than silently dropping the message event.
                                    await _hub.Clients.Group(chatGuid.ToString()).SendAsync("NewMessage", payload, cancellationToken: stoppingToken);
                                }
                            }

                            await outbox.MarkSentAsync(item);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to dispatch outbox item {OutboxId}", item.Id);
                            // Increment attempts and possibly DLQ
                            item.IncrementAttempt(ex.Message);
                            await outbox.UpdateAsync(item);

                            if (item.DeadLetterAt.HasValue)
                            {
                                // send notification to admins about dead letter
                                try
                                {
                                    await _hub.Clients.Group("admins").SendAsync("OutboxDeadLetter", item.Payload, item.LastError);
                                }
                                catch (Exception notifEx)
                                {
                                    _logger.LogError(notifEx, "Failed to notify admins for dead letter {OutboxId}", item.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OutboxDispatcher loop");
                }

                // Wait for either a "something was just enqueued" signal or the
                // poll interval, whichever comes first. This used to be a flat
                // Task.Delay(5s), which meant every message waited up to five
                // seconds (2.5s on average) in the table before being pushed
                // out over SignalR — the visible lag between sending a message
                // and it appearing for anyone, sender included.
                await _signal.WaitAsync(PollInterval, stoppingToken);
            }
        }
    }
}
