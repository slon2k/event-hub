using EventHub.Domain.Common;
using EventHub.Domain.Enumerations;

namespace EventHub.Domain.Entities;

/// <summary>
/// Represents a single invitation sent to a participant email address.
/// Participants are guests — no account is required.
/// RSVP is authorized by the Application layer validating the magic link token
/// (via IRsvpTokenService) before calling Accept() or Decline().
/// </summary>
public sealed class Invitation : Entity
{
    public Guid EventId { get; private set; }

    /// <summary>Email address of the invitee. Unique constraint per event enforced at aggregate level.</summary>
    public string ParticipantEmail { get; private set; } = default!;

    public InvitationStatus Status { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>
    /// HMAC-SHA256 hash of the raw RSVP token.
    /// The raw token is sent only in the invitation email and is never persisted.
    /// Cleared on use (single-use) or when the invitation is cancelled.
    /// </summary>
    public string? RsvpTokenHash { get; private set; }

    /// <summary>UTC expiry of the RSVP token. Null once consumed or cancelled.</summary>
    public DateTimeOffset? RsvpTokenExpiresAt { get; private set; }

    private Invitation() { } // EF Core

    internal static Invitation Create(
        Guid eventId,
        string participantEmail,
        string tokenHash,
        DateTimeOffset tokenExpiresAt)
    {
        return new Invitation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ParticipantEmail = participantEmail.ToLowerInvariant(),
            Status = InvitationStatus.Pending,
            SentAt = DateTimeOffset.UtcNow,
            RsvpTokenHash = tokenHash,
            RsvpTokenExpiresAt = tokenExpiresAt
        };
    }

    /// <summary>
    /// Transitions to Accepted. The Application layer is responsible for validating
    /// the magic link token via IRsvpTokenService before calling this method.
    /// </summary>
    internal void Accept()
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException(
                $"Cannot accept invitation in status '{Status}'. Only Pending invitations can be accepted.");

        Status = InvitationStatus.Accepted;
        RespondedAt = DateTimeOffset.UtcNow;
        ClearToken();
    }

    /// <summary>
    /// Transitions to Declined. The Application layer is responsible for validating
    /// the magic link token via IRsvpTokenService before calling this method.
    /// </summary>
    internal void Decline()
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException(
                $"Cannot decline invitation in status '{Status}'. Only Pending invitations can be declined.");

        Status = InvitationStatus.Declined;
        RespondedAt = DateTimeOffset.UtcNow;
        ClearToken();
    }

    /// <summary>Cancels the invitation and invalidates the magic link token.</summary>
    internal void Cancel()
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException(
                $"Cannot cancel invitation in status '{Status}'. Only Pending invitations can be cancelled.");

        Status = InvitationStatus.Cancelled;
        ClearToken();
    }

    /// <summary>
    /// Reissues a fresh token. Used by the organizer when the original token has expired.
    /// </summary>
    internal void ReissueToken(string newTokenHash, DateTimeOffset newExpiresAt)
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException(
                $"Cannot reissue token for invitation in status '{Status}'.");

        RsvpTokenHash = newTokenHash;
        RsvpTokenExpiresAt = newExpiresAt;
    }

    private void ClearToken()
    {
        RsvpTokenHash = null;
        RsvpTokenExpiresAt = null;
    }
}
