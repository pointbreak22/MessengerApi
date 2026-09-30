using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Repositories;

namespace WebAPI.Services
{
    /// <summary>
    /// Process-local wake-up channel between message writers and
    /// <see cref="OutboxDispatcher"/>. Registered as a singleton.
    /// </summary>
    public sealed class OutboxSignal : IOutboxSignal
    {
        // Capacity of exactly one: a burst of messages should wake the loop
        // once, not queue up a wake-up per message. The dispatcher drains the
        // whole pending batch each pass anyway, so extra iterations would just
        // be empty round trips to the database.
        private readonly SemaphoreSlim _semaphore = new(0, 1);

        public void Notify()
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // A wake-up is already queued and not yet consumed — nothing
                // to add.
            }
        }

        /// <summary>
        /// Completes as soon as someone calls <see cref="Notify"/>, or after
        /// <paramref name="timeout"/> elapses — whichever comes first. The
        /// timeout is what keeps the dispatcher polling when no notification
        /// arrives (rows written by another instance, or a lost signal).
        /// </summary>
        public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
            => _semaphore.WaitAsync(timeout, cancellationToken);
    }
}
