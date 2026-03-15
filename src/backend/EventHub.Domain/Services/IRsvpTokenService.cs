namespace EventHub.Domain.Services;

/// <summary>
/// Generates and validates HMAC-SHA256 signed RSVP tokens for guest participants.
/// </summary>
public interface IRsvpTokenService
{
    /// <summary>
    /// Generates a new raw token and its HMAC-SHA256 hash.
    /// The raw token is sent in the invitation email; only the hash is persisted.
    /// </summary>
    /// <param name="invitationId">Scopes the token to a specific invitation.</param>
    /// <param name="email">Scopes the token to the intended recipient.</param>
    /// <param name="expiresAt">UTC expiry time embedded in the token payload.</param>
    /// <returns>A tuple of the raw (unstorable) token and its hash (to be persisted).</returns>
    (string RawToken, string TokenHash) Generate(Guid invitationId, string email, DateTimeOffset expiresAt);

    /// <summary>
    /// Validates that a raw token matches a stored hash and has not expired.
    /// </summary>
    /// <param name="rawToken">The token received from the participant's RSVP request.</param>
    /// <param name="storedHash">The hash stored on the Invitation row.</param>
    /// <param name="expiresAt">The expiry stored on the Invitation row.</param>
    bool IsValid(string rawToken, string storedHash, DateTimeOffset expiresAt);
}
