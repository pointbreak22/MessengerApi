using System;

namespace Domain.Entities
{
    // One row per (MessageId, UserId) — a user has at most one active emoji
    // reaction per message, matching the tap-to-toggle UX (pick a new emoji
    // to replace, tap the same one again to remove).
    public class MessageReaction
    {
        public Guid Id { get; private set; }
        public Guid MessageId { get; private set; }
        public string UserId { get; private set; }
        public string Emoji { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private MessageReaction() { }

        public static MessageReaction Create(Guid messageId, string userId, string emoji)
        {
            if (string.IsNullOrWhiteSpace(emoji))
                throw new ArgumentException("Emoji is required.");

            return new MessageReaction
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void UpdateEmoji(string emoji)
        {
            if (string.IsNullOrWhiteSpace(emoji))
                throw new ArgumentException("Emoji is required.");
            Emoji = emoji;
        }
    }
}
