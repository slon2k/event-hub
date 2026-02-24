using EventHub.Domain.Common;
using EventHub.Domain.Enumerations;
using EventHub.Domain.Events;

namespace EventHub.Domain.Entities;

/// <summary>
/// Aggregate root for the event management bounded context.
/// All mutations to Invitation entities must go through this aggregate root.
/// </summary>
public sealed class Event : AggregateRoot
{
    public string Title { get; private set; } = default!;

    public string? Description { get; private set; }

    public DateTimeOffset DateTime { get; private set; }

    public string? Location { get; private set; }

    /// <summary>Maximum number of accepted RSVPs. Null means unlimited.</summary>
    public int? Capacity { get; private set; }

    public EventStatus Status { get; private set; }

    /// <summary>Entra ID Object ID of the organizer who created this event.</summary>
    public string OrganizerId { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Invitation> _invitations = [];
    public IReadOnlyCollection<Invitation> Invitations => _invitations.AsReadOnly();

    private Event() { } // EF Core

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Event Create(
        string title,
        string? description,
        DateTimeOffset dateTime,
        string? location,
        int? capacity,
        string organizerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizerId);

        if (dateTime <= DateTimeOffset.UtcNow)
            throw new DomainException("Event date must be in the future.");

        if (capacity is <= 0)
            throw new DomainException("Event capacity must be a positive number.");

        var now = DateTimeOffset.UtcNow;

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description?.Trim(),
            DateTime = dateTime,
            Location = location?.Trim(),
            Capacity = capacity,
            Status = EventStatus.Draft,
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ── Event lifecycle ───────────────────────────────────────────────────────

    public void Update(
        string title,
        string? description,
        DateTimeOffset dateTime,
        string? location,
        int? capacity)
    {
        EnsureNotCancelled();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (dateTime <= DateTimeOffset.UtcNow)
            throw new DomainException("Event date must be in the future.");

        if (capacity is <= 0)
            throw new DomainException("Event capacity must be a positive number.");

        Title = title.Trim();
        Description = description?.Trim();
        DateTime = dateTime;
        Location = location?.Trim();
        Capacity = capacity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Publish()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException(
                $"Only Draft events can be published. Current status: '{Status}'.");

        Status = EventStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        EnsureNotCancelled();

        var affectedEmails = _invitations
            .Where(i => i.Status is InvitationStatus.Pending or InvitationStatus.Accepted)
            .Select(i => i.ParticipantEmail)
            .ToList();

        Status = EventStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new EventCancelled(Id, Title, DateTime, affectedEmails));
    }

    // ── Invitation management ─────────────────────────────────────────────────

    /// <summary>
    /// Sends an invitation to a participant email.
    /// The Application handler generates the token pair via IRsvpTokenService
    /// and passes both the raw token (for the outbox/email) and the hash (for storage).
    /// </summary>
    public Invitation AddInvitation(
        string participantEmail,
        string rawToken,
        string tokenHash,
        DateTimeOffset tokenExpiresAt,
        Guid? invitationId = null)
    {
        if (Status != EventStatus.Published)
            throw new DomainException(
                $"Invitations can only be sent for Published events. Current status: '{Status}'.");

        var normalizedEmail = participantEmail.ToLowerInvariant();

        var duplicate = _invitations.FirstOrDefault(
            i => i.ParticipantEmail == normalizedEmail
              && i.Status != InvitationStatus.Cancelled);

        if (duplicate is not null)
            throw new DomainException(
                $"An active invitation for '{participantEmail}' already exists for this event.");

        var invitation = Invitation.Create(Id, normalizedEmail, tokenHash, tokenExpiresAt, invitationId);
        _invitations.Add(invitation);

        RaiseDomainEvent(new InvitationSent(
            Id, Title, DateTime, Location,
            invitation.Id, normalizedEmail,
            rawToken, tokenExpiresAt));

        return invitation;
    }

    /// <summary>
    /// Cancels a Pending invitation and invalidates its magic link token.
    /// </summary>
    public void CancelInvitation(Guid invitationId)
    {
        EnsureNotCancelled();
        var invitation = GetPendingInvitation(invitationId);
        invitation.Cancel();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Accepts a Pending invitation. Enforces capacity before transitioning.
    /// The Application layer validates the magic link token before calling this method.
    /// </summary>
    public void AcceptInvitation(Guid invitationId)
    {
        EnsureNotCancelled();

        if (Capacity.HasValue && AcceptedCount >= Capacity.Value)
            throw new DomainException(
                "This event has reached its maximum capacity and no further acceptances can be made.");

        var invitation = GetPendingInvitation(invitationId);
        invitation.Accept();
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new InvitationResponded(Id, invitation.Id, invitation.ParticipantEmail, InvitationStatus.Accepted));
    }

    /// <summary>
    /// Declines a Pending invitation.
    /// The Application layer validates the magic link token before calling this method.
    /// </summary>
    public void DeclineInvitation(Guid invitationId)
    {
        EnsureNotCancelled();
        var invitation = GetPendingInvitation(invitationId);
        invitation.Decline();
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new InvitationResponded(Id, invitation.Id, invitation.ParticipantEmail, InvitationStatus.Declined));
    }

    /// <summary>
    /// Reissues a fresh magic link token for a Pending invitation whose token has expired.
    /// </summary>
    public void ReissueInvitationToken(Guid invitationId, string rawToken, string newTokenHash, DateTimeOffset newExpiresAt)
    {
        EnsureNotCancelled();
        var invitation = GetPendingInvitation(invitationId);
        invitation.ReissueToken(newTokenHash, newExpiresAt);
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new InvitationSent(
            Id, Title, DateTime, Location,
            invitation.Id, invitation.ParticipantEmail,
            rawToken, newExpiresAt));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private int AcceptedCount =>
        _invitations.Count(i => i.Status == InvitationStatus.Accepted);

    private Invitation GetPendingInvitation(Guid invitationId)
    {
        var invitation = _invitations.FirstOrDefault(i => i.Id == invitationId)
            ?? throw new DomainException($"Invitation '{invitationId}' not found.");

        if (invitation.Status != InvitationStatus.Pending)
            throw new DomainException(
                $"Invitation '{invitationId}' is in status '{invitation.Status}' and cannot be modified.");

        return invitation;
    }

    private void EnsureNotCancelled()
    {
        if (Status == EventStatus.Cancelled)
            throw new DomainException("Cannot modify a cancelled event.");
    }
}
