using System.Security.Claims;

namespace EventHub.Api.Extensions;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the Entra ID Object ID (oid claim) which is used as the organizer/user identifier.
    /// Falls back to the standard NameIdentifier claim for non-Entra ID tokens.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated user has no resolvable ID claim.");
    }
}
