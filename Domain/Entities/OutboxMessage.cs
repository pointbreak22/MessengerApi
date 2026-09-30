using System;
using System.Text.Json;

namespace Domain.Entities
{
    public class OutboxMessage
    {
        public Guid Id { get; private set; }
        public string Type { get; private set; } = null!;
        public string Payload { get; private set; } = null!; // JSON payload
        public DateTime OccurredAt { get; private set; }
        public DateTime? SentAt { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? LastAttemptAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? DeadLetterAt { get; private set; }
        public string? LastError { get; private set; }

        private OutboxMessage() { }

        public static OutboxMessage Create(string type, string payload, string? idempotencyKey = null)
        {
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = type,
                Payload = payload,
                OccurredAt = DateTime.UtcNow,
                IdempotencyKey = idempotencyKey
            };
        }

        public void MarkSent()
        {
            SentAt = DateTime.UtcNow;
        }

        public void IncrementAttempt(string? lastError = null)
        {
            Attempts++;
            LastAttemptAt = DateTime.UtcNow;
            LastError = lastError;

            // exponential backoff in seconds (2^attempts)
            var delaySeconds = Math.Pow(2, Attempts);
            NextAttemptAt = DateTime.UtcNow.AddSeconds(delaySeconds);

            if (Attempts >= 5)
            {
                DeadLetterAt = DateTime.UtcNow;
            }
        }
    }
}
