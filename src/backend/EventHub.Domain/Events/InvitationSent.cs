using EventHub.Domain.Common;

namespace EventHub.Domain.Events;

/// <summary>
/// Raised when an organizer sends a new invitation to a participant.
/// The raw RSVP token is included so the Application layer can embed it
/// in the Outbox message for ACS Email delivery.
/// The raw token is NEVER persisted to the database — only its hash is stored on the Invitation.
/// </summary>
public sealed record InvitationSent(
    Guid EventId,
    string EventTitle,
    DateTimeOffset EventDateTime,
    string? EventLocation,
    Guid InvitationId,
    string ParticipantEmail,
    string RsvpToken,
    DateTimeOffset TokenExpiresAt
) : IDomainEvent;
