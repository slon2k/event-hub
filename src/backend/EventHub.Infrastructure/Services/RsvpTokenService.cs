using System.Security.Cryptography;
using System.Text;
using EventHub.Domain.Services;

namespace EventHub.Infrastructure.Services;

/// <summary>
/// Generates and validates RSVP magic-link tokens.
///
/// Generation:  HMAC-SHA256(key, "{invitationId}:{email}:{expiresAt_unix_seconds}")
///              Raw token  = base64-url encoding of the 32-byte HMAC digest
///              Token hash = lower-hex SHA-256 of the UTF-8 raw token string
///              (only the hash is persisted; the raw token travels only in the email)
///
/// Validation:  re-hash the supplied raw token and compare to the stored hash
///              using a constant-time comparison to prevent timing attacks.
/// </summary>
public sealed class RsvpTokenService(string base64Key) : IRsvpTokenService
{
    private readonly byte[] _key = Convert.FromBase64String(base64Key);

    public (string RawToken, string TokenHash) Generate(
        Guid invitationId,
        string email,
        DateTimeOffset expiresAt)
    {
        var message = $"{invitationId}:{email.ToLowerInvariant()}:{expiresAt.ToUnixTimeSeconds()}";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        var hmacBytes = HMACSHA256.HashData(_key, messageBytes);
        var rawToken = Convert.ToBase64String(hmacBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url

        var tokenHash = ComputeHash(rawToken);
        return (rawToken, tokenHash);
    }

    public bool IsValid(string rawToken, string storedHash, DateTimeOffset expiresAt)
    {
        if (DateTimeOffset.UtcNow >= expiresAt)
            return false;

        var computedHash = ComputeHash(rawToken);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash);

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    private static string ComputeHash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
