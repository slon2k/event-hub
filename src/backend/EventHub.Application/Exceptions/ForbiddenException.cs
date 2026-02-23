namespace EventHub.Application.Exceptions;

/// <summary>Thrown when the caller lacks permission to perform an operation. Maps to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
