namespace EventHub.Domain.Enumerations;

public enum EventStatus
{
    /// <summary>Created but not yet visible to participants. Cannot send invitations.</summary>
    Draft,

    /// <summary>Active. Invitations can be sent.</summary>
    Published,

    /// <summary>Permanently deactivated. Cannot be reactivated. Triggers cancellation notifications.</summary>
    Cancelled
}
