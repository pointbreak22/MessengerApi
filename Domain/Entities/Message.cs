using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Message
    {
        public Guid Id { get; private set; }
        public Guid ChatId { get; private set; }
        public string SenderId { get; private set; }
        public string Text { get; private set; }
        public string? AttachmentUrl { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? EditedAt { get; private set; }
        public Guid? ReplyToMessageId { get; private set; }

        private Message() { }

        public static Message Create(Guid chatId, string senderId, string text, string? attachmentUrl = null, Guid? replyToMessageId = null)
        {
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(attachmentUrl))
                throw new ArgumentException("Сообщение должно содержать текст или вложение.");

            return new Message
            {
                Id               = Guid.NewGuid(),
                ChatId           = chatId,
                SenderId         = senderId,
                Text             = text ?? string.Empty,
                AttachmentUrl    = attachmentUrl,
                CreatedAt        = DateTime.UtcNow,
                ReplyToMessageId = replyToMessageId
            };
        }

        // Only the sender may call this — enforced by the caller
        // (EditMessageCommandHandler), not here.
        public void UpdateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(AttachmentUrl))
                throw new ArgumentException("Сообщение должно содержать текст или вложение.");

            Text = text ?? string.Empty;
            EditedAt = DateTime.UtcNow;
        }
    }
}
