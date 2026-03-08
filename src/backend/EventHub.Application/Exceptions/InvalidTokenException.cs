namespace EventHub.Application.Exceptions;

/// <summary>Thrown when an RSVP token is invalid, expired, or not found. Maps to HTTP 400.</summary>
public sealed class InvalidTokenException(string message) : Exception(message);
