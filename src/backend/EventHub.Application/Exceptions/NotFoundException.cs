namespace EventHub.Application.Exceptions;

/// <summary>Thrown when a requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.");
