using EventHub.Domain.Common;
using EventHub.Domain.Enumerations;

namespace EventHub.Domain.Events;

/// <summary>
/// Raised when a participant accepts or declines an invitation via the magic link.
/// </summary>
public sealed record InvitationResponded(
    Guid EventId,
    Guid InvitationId,
    string ParticipantEmail,
    InvitationStatus Response
) : IDomainEvent;
