using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EventHub.Api.FunctionalTests;

internal static class TokenHelper
{
    private static readonly SymmetricSecurityKey Key =
        new(Convert.FromBase64String(ApiFactory.TestHmacKeyBase64));

    /// <summary>
    /// Mints a signed JWT for the given organizer ID with the Organizer role.
    /// The <paramref name="organizerId"/> is placed in the "oid" claim, which is
    /// what <c>ClaimsPrincipalExtensions.GetUserId()</c> extracts.
    /// </summary>
    public static string OrganizerToken(string organizerId) =>
        CreateToken(organizerId, "Organizer");

    public static string AdminToken(string adminId = "admin-001") =>
        CreateToken(adminId, "Admin");

    private static string CreateToken(string userId, string role)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("oid", userId),
                new Claim("roles", role),
            ]),
            Issuer = ApiFactory.TestIssuer,
            Audience = ApiFactory.TestAudience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
