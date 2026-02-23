namespace EventHub.Domain.Enumerations;

public enum InvitationStatus
{
    /// <summary>Sent, awaiting participant response via magic link.</summary>
    Pending,

    /// <summary>Participant accepted via magic link.</summary>
    Accepted,

    /// <summary>Participant declined via magic link.</summary>
    Declined,

    /// <summary>Cancelled by the organizer. Magic link token cleared.</summary>
    Cancelled
}
