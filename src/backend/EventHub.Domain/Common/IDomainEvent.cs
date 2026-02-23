namespace EventHub.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// Domain events are raised inside aggregate methods and dispatched
/// by the Application layer after SaveChanges() succeeds.
/// </summary>
public interface IDomainEvent;
