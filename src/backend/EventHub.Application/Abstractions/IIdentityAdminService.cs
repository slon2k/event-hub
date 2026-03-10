using EventHub.Application.Common;
using EventHub.Application.Features.Admin;

namespace EventHub.Application.Abstractions;

/// <summary>
/// Abstraction over the identity provider for admin user management.
/// Implemented in Infrastructure (e.g., via Microsoft Graph).
/// Keeps Graph-specific details entirely out of Application layer handlers.
/// </summary>
public interface IIdentityAdminService
{
    Task<PagedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct);
    Task AssignOrganizerRoleAsync(string targetUserId, string actingAdminUserId, CancellationToken ct);
    Task RemoveOrganizerRoleAsync(string targetUserId, string actingAdminUserId, CancellationToken ct);
}
