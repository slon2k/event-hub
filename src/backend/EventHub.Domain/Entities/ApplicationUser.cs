namespace EventHub.Domain.Entities;

/// <summary>
/// Local mirror of an Entra ID user (Admin or Organizer).
/// Created/updated on first authenticated request from JWT claims.
/// Participants are guests — they do NOT have ApplicationUser records.
/// </summary>
public sealed class ApplicationUser
{
    /// <summary>Entra ID Object ID (OID claim). Used as the primary key.</summary>
    public string Id { get; private set; } = default!;

    public string Email { get; private set; } = default!;

    public string DisplayName { get; private set; } = default!;

    private ApplicationUser() { } // EF Core

    public static ApplicationUser Create(string entraOid, string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entraOid);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new ApplicationUser
        {
            Id = entraOid,
            Email = email,
            DisplayName = displayName
        };
    }

    public void Update(string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Email = email;
        DisplayName = displayName;
    }
}
