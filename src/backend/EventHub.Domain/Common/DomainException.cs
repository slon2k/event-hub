namespace EventHub.Domain.Common;

/// <summary>
/// Thrown when an aggregate invariant or business rule is violated.
/// Should be caught at the API boundary and surfaced as a 422 or 409 response.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
