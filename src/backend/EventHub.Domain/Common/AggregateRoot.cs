namespace EventHub.Domain.Common;

/// <summary>
/// Marker base class for aggregate roots.
/// Only aggregate roots may be loaded directly from repositories.
/// Entities owned by an aggregate are accessed exclusively through their aggregate root.
/// </summary>
public abstract class AggregateRoot : Entity;
