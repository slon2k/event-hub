namespace EventHub.Application.Abstractions;

/// <summary>
/// Signals that new outbox messages are ready to be picked up by the processor.
/// Allows the API to wake the Azure Function on demand, replacing constant timer-based DB polling
/// </summary>
public interface IOutboxNotifier
{
    /// <summary>
    /// Sends a wake-up signal. Implementations must swallow their own exceptions
    /// so a notifier failure never rolls back the domain transaction.
    /// </summary>
    Task NotifyAsync(CancellationToken cancellationToken = default);
}
