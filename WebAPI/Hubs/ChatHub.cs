using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Domain.Repositories;
using WebAPI.Services;

namespace WebAPI.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMediator _mediator;
        private readonly IPresenceService _presenceService;
        private readonly IActiveCallService _activeCalls;
        private readonly IChatRepository _chats;

        public ChatHub(IMediator mediator, IPresenceService presenceService, IActiveCallService activeCalls, IChatRepository chats)
        {
            _mediator = mediator;
            _presenceService = presenceService;
            _activeCalls = activeCalls;
            _chats = chats;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await _presenceService.AddConnectionAsync(userId, Context.ConnectionId);
                await Clients.Others.SendAsync("UserWentOnline", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                var wentOffline = await _presenceService.RemoveConnectionAsync(userId, Context.ConnectionId);
                if (wentOffline)
                    await Clients.Others.SendAsync("UserWentOffline", userId);

                // Best-effort — a dropped connection (closed tab, lost network)
                // shouldn't leave a ghost participant sitting in a call room.
                // Genuinely best-effort: if Redis hiccups, this must not take
                // down disconnect handling for every user on the hub (1:1
                // callers included) just because one of them happened to be
                // in a group call room.
                try
                {
                    var activeCall = await _activeCalls.GetUserCallAsync(userId);
                    if (activeCall != null)
                        await LeaveGroupCallInternal(activeCall.Value.CallId, userId);
                }
                catch { }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Отправить сообщение через WebSocket. Альтернатива REST POST /api/messages/{chatId}.
        // Используй REST если нужна гарантия доставки при нестабильном соединении.
        public async Task SendMessage(string chatId, string text, string? attachmentUrl = null, string? idempotencyKey = null, string? replyToMessageId = null)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;
            if (!Guid.TryParse(chatId, out var chatGuid)) return;
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(attachmentUrl)) return;
            Guid.TryParse(replyToMessageId, out var replyGuid);

            await _mediator.Send(new Application.CQRS.Messages.Commands.SendMessageCommand(
                chatGuid, userId, text ?? string.Empty, idempotencyKey, attachmentUrl,
                replyToMessageId != null ? replyGuid : null));
        }

        // Ephemeral "user is typing" signal — fire-and-forget, no persistence,
        // no MediatR round-trip (would be overkill for something this frequent
        // and disposable). Per-member like everything else here, not
        // Clients.Group, so it still reaches someone who has this chat open
        // in another tab/device even if that tab never "joined" the room.
        public async Task Typing(string chatId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;
            if (!Guid.TryParse(chatId, out var chatGuid)) return;

            var chat = await _chats.GetByIdAsync(chatGuid);
            if (chat == null) return;

            foreach (var member in chat.Members)
            {
                if (member.UserId == userId) continue;
                try
                {
                    await Clients.User(member.UserId).SendAsync("UserTyping", new { ChatId = chatId, UserId = userId });
                }
                catch { }
            }
        }

        // Сигналинг звонков (1:1, только внутри личных чатов). Сервер медиа не трогает —
        // просто пересылает SDP/ICE как есть от инициатора к targetUserId, тем же паттерном,
        // что уже используется для FriendRequestAccepted/AddedToGroup.
        // Returns the generated callId — registered in IActiveCallService right
        // away (same store group calls use) so 1:1 calls show up in the same
        // busy/red-yellow presence badges everywhere, not just group calls.
        public async Task<string> CallUser(string targetUserId, string chatId, string offerSdp, bool isVideo)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return string.Empty;
            if (!Guid.TryParse(chatId, out var chatGuid)) return string.Empty;

            var allowed = await _mediator.Send(new Application.CQRS.Chats.Queries.CanInitiateCallQuery(chatGuid, userId, targetUserId));
            if (!allowed) return string.Empty;

            var callId = Guid.NewGuid().ToString();
            await _activeCalls.JoinAsync(callId, chatId, userId, isVideo);
            await BroadcastCallStateAsync(userId);

            await Clients.User(targetUserId).SendAsync("IncomingCall", new
            {
                CallId = callId,
                FromUserId = userId,
                ChatId = chatId,
                OfferSdp = offerSdp,
                IsVideo = isVideo
            });

            return callId;
        }

        public async Task AnswerCall(string targetUserId, string callId, string answerSdp)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            // Meta was stored by CallUser above — reuse it rather than trust
            // whatever the answering client claims chatId/isVideo are.
            var meta = await _activeCalls.GetCallMetaAsync(callId);
            if (meta != null)
            {
                await _activeCalls.JoinAsync(callId, meta.Value.ChatId, userId, meta.Value.IsVideo);
                await BroadcastCallStateAsync(userId);
            }

            await Clients.User(targetUserId).SendAsync("CallAnswered", new
            {
                FromUserId = userId,
                AnswerSdp = answerSdp
            });

            // Same reasoning as DeclineCall — my other tabs/devices should
            // stop ringing for this call now that I've answered it somewhere.
            await Clients.User(userId).SendAsync("IncomingCallResolved", new { CallId = callId });
        }

        public async Task SendIceCandidate(string targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.User(targetUserId).SendAsync("IceCandidateReceived", new
            {
                FromUserId = userId,
                Candidate = candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = sdpMLineIndex
            });
        }

        public async Task DeclineCall(string targetUserId, string callId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            // CallId included so the caller can tell a stale/duplicate decline
            // (e.g. replayed after a SignalR auto-reconnect on a flaky
            // connection) apart from one that actually belongs to whatever
            // call it's currently on — without this, a late DeclineCall could
            // tear down a call that already connected via a fresher AnswerCall.
            await Clients.User(targetUserId).SendAsync("CallDeclined", new { FromUserId = userId, CallId = callId });

            // Fans out to every connection I have (tabs/devices) — if I
            // declined from one, the others should stop ringing for the same
            // call instead of sitting there indefinitely.
            await Clients.User(userId).SendAsync("IncomingCallResolved", new { CallId = callId });

            // Best-effort — leaves a "Missed call" notice in chat history like
            // every other messenger. Encoded as a plain-text marker on a real
            // persisted Message rather than a new column: reuses the whole
            // existing send/outbox/NewMessage pipeline for free, and the
            // client (shared/call-event-display.ts) parses it back out for
            // its own rendering instead of showing raw "[call:...]" text.
            // Skipped if I'm already registered on this call's room — means
            // this DeclineCall is itself stale (I actually answered via a
            // different/fresher path already), so there's nothing to report.
            try
            {
                var meta = await _activeCalls.GetCallMetaAsync(callId);
                var participants = await _activeCalls.GetParticipantsAsync(callId);
                if (meta != null && !participants.Contains(userId))
                {
                    await _mediator.Send(new Application.CQRS.Messages.Commands.SendMessageCommand(
                        Guid.Parse(meta.Value.ChatId), targetUserId, $"[call:declined:{(meta.Value.IsVideo ? "video" : "audio")}]"));
                }
            }
            catch { }
        }

        // Best-effort presence cleanup — called by the client whenever its own
        // call state resets to idle (hangup, decline, remote end, ICE failure),
        // regardless of which of those it was, so a 1:1 call never leaves a
        // ghost "busy" entry behind.
        public async Task LeaveCall(string callId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            try
            {
                await _activeCalls.LeaveAsync(callId, userId);
                await BroadcastCallStateAsync(userId, left: true);
            }
            catch { }
        }

        public async Task EndCall(string targetUserId, string? callId = null)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.User(targetUserId).SendAsync("CallEnded", new { FromUserId = userId, CallId = callId });

            // Only post "Missed call" if the callee never actually joined the
            // call room — i.e. the caller gave up before it was answered.
            // Called before the caller's own LeaveCall cleanup runs (see
            // CallService.hangUp()/resetCall() ordering client-side), so at
            // this point the room still reflects who was really on the call.
            if (!string.IsNullOrEmpty(callId))
            {
                try
                {
                    var meta = await _activeCalls.GetCallMetaAsync(callId);
                    var participants = await _activeCalls.GetParticipantsAsync(callId);
                    if (meta != null && participants.Count < 2)
                    {
                        await _mediator.Send(new Application.CQRS.Messages.Commands.SendMessageCommand(
                            Guid.Parse(meta.Value.ChatId), userId, $"[call:missed:{(meta.Value.IsVideo ? "video" : "audio")}]"));
                    }
                }
                catch { }
            }
        }

        // ==================== Групповые звонки (mesh WebRTC, N участников) ====================
        // Тот же принцип, что и выше для 1:1: сервер не трогает медиа, только пересылает
        // SDP/ICE между участниками. Каждый broadcast адресован явным списком userId через
        // Clients.Users(...), а не Clients.Group — иначе событие не дойдёт до тех, кто прямо
        // сейчас не сидит в этой ad-hoc SignalR-группе (та же причина, по которой NewMessage
        // в OutboxDispatcher был переведён с Clients.Group на Clients.Users).

        // Returns the generated callId — the caller needs it too (to manage
        // its own room state), not just the invitees via IncomingGroupCall.
        public async Task<string> StartGroupCall(string chatId, List<string> participantUserIds, bool isVideo)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return string.Empty;
            if (!Guid.TryParse(chatId, out var chatGuid)) return string.Empty;

            var allowed = await _mediator.Send(
                new Application.CQRS.Chats.Queries.CanInitiateGroupCallQuery(chatGuid, userId, participantUserIds));
            if (!allowed) return string.Empty;

            var callId = Guid.NewGuid().ToString();
            await _activeCalls.JoinAsync(callId, chatId, userId, isVideo);
            await BroadcastCallStateAsync(userId);

            await Clients.Users(participantUserIds).SendAsync("IncomingGroupCall", new
            {
                CallId = callId,
                ChatId = chatId,
                FromUserId = userId,
                IsVideo = isVideo
            });

            return callId;
        }

        // Invite one more person into a call that's already running — sends the
        // exact same IncomingGroupCall the original invitees got, just for an
        // existing callId instead of a freshly minted one, so accepting it
        // (JoinGroupCall) lands them in the same room. Only an actual current
        // participant can invite, and only an actual chat member can be invited.
        public async Task InviteToGroupCall(string callId, string chatId, string targetUserId, bool isVideo)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;
            if (!Guid.TryParse(chatId, out var chatGuid)) return;

            var participants = await _activeCalls.GetParticipantsAsync(callId);
            if (!participants.Contains(userId)) return;

            var allowed = await _mediator.Send(
                new Application.CQRS.Chats.Queries.CanInitiateGroupCallQuery(chatGuid, userId, new List<string> { targetUserId }));
            if (!allowed) return;

            await Clients.User(targetUserId).SendAsync("IncomingGroupCall", new
            {
                CallId = callId,
                ChatId = chatId,
                FromUserId = userId,
                IsVideo = isVideo
            });
        }

        // Accepts an invite AND powers "join an already-ongoing call" — same method either way.
        public async Task JoinGroupCall(string callId, string chatId, bool isVideo)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            var existing = await _activeCalls.GetParticipantsAsync(callId);
            await _activeCalls.JoinAsync(callId, chatId, userId, isVideo);
            await BroadcastCallStateAsync(userId);

            // Tell the joiner who's already here — they initiate offers to each
            // existing participant, so nobody double-offers the same peer.
            await Clients.Caller.SendAsync("GroupCallRoster", new
            {
                CallId = callId,
                ChatId = chatId,
                IsVideo = isVideo,
                ParticipantIds = existing
            });

            if (existing.Count > 0)
                await Clients.Users(existing.ToList()).SendAsync("ParticipantJoined", new { CallId = callId, UserId = userId });
        }

        public async Task LeaveGroupCall(string callId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;
            await LeaveGroupCallInternal(callId, userId);
        }

        // Declining an invite before ever joining — nothing to remove from the
        // room roster, just let the caller/others know so a ringing UI can stop.
        public async Task DeclineGroupCall(string callId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            var participants = await _activeCalls.GetParticipantsAsync(callId);
            if (participants.Count > 0)
                await Clients.Users(participants.ToList()).SendAsync("ParticipantDeclined", new { CallId = callId, UserId = userId });
        }

        public async Task SendGroupOffer(string callId, string targetUserId, string sdp)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.User(targetUserId).SendAsync("GroupOfferReceived", new { CallId = callId, FromUserId = userId, Sdp = sdp });
        }

        public async Task SendGroupAnswer(string callId, string targetUserId, string sdp)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.User(targetUserId).SendAsync("GroupAnswerReceived", new { CallId = callId, FromUserId = userId, Sdp = sdp });
        }

        public async Task SendGroupIceCandidate(string callId, string targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Clients.User(targetUserId).SendAsync("GroupIceCandidateReceived", new
            {
                CallId = callId,
                FromUserId = userId,
                Candidate = candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = sdpMLineIndex
            });
        }

        // Explicit mute/camera signaling instead of relying on WebRTC track
        // mute/unmute events on the receiving end — more reliable for the
        // per-tile status icons in the group call grid.
        public async Task SendGroupMediaState(string callId, bool micMuted, bool cameraOff)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            var participants = await _activeCalls.GetParticipantsAsync(callId);
            var others = participants.Where(p => p != userId).ToList();
            if (others.Count == 0) return;

            await Clients.Users(others).SendAsync("ParticipantMediaStateChanged", new
            {
                CallId = callId,
                UserId = userId,
                MicMuted = micMuted,
                CameraOff = cameraOff
            });
        }

        private async Task LeaveGroupCallInternal(string callId, string userId)
        {
            await _activeCalls.LeaveAsync(callId, userId);
            await BroadcastCallStateAsync(userId, left: true);

            var remaining = await _activeCalls.GetParticipantsAsync(callId);
            if (remaining.Count > 0)
                await Clients.Users(remaining.ToList()).SendAsync("ParticipantLeft", new { CallId = callId, UserId = userId });
        }

        // Fans out to everyone else (unscoped, same convention as UserWentOnline/
        // UserWentOffline above) so presence badges — red "in a call with me",
        // yellow "in some other call" — update live wherever that user's avatar
        // is rendered, not just within one chat.
        private async Task BroadcastCallStateAsync(string userId, bool left = false)
        {
            var current = left ? null : await _activeCalls.GetUserCallAsync(userId);
            await Clients.Others.SendAsync("UserCallStateChanged", new
            {
                UserId = userId,
                ChatId = current?.ChatId,
                CallId = current?.CallId,
                IsVideo = current?.IsVideo ?? false
            });
        }

        public async Task JoinChatRoom(string chatId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        }

        public async Task LeaveChatRoom(string chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
        }
    }
}
