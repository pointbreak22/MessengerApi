namespace Domain.Repositories
{
    /// <summary>
    /// Lets whoever just wrote an outbox row tell the dispatcher to run now
    /// instead of sleeping until its next poll. Without it a message sits in
    /// the table for up to the full poll interval before anyone is notified,
    /// which is exactly the delay users see between pressing send and the
    /// message showing up.
    ///
    /// Purely an optimisation: the dispatcher still polls on a timer, so a
    /// missed or lost notification only costs latency, never delivery. That
    /// also covers the multi-instance case, where the row is written by one
    /// instance and picked up by another whose in-memory signal never fired.
    /// </summary>
    public interface IOutboxSignal
    {
        void Notify();
    }
}
