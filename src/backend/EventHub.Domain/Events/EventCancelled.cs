using EventHub.Domain.Common;

namespace EventHub.Domain.Events;

/// <summary>
/// Raised when an organizer cancels an event.
/// Contains the emails of all participants with Pending or Accepted invitations
/// so the Application layer can send cancellation notifications.
/// </summary>
public sealed record EventCancelled(
    Guid EventId,
    string EventTitle,
    DateTimeOffset EventDateTime,
    IReadOnlyList<string> AffectedParticipantEmails
) : IDomainEvent;
