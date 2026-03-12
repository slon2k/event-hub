using EventHub.Application.Abstractions;
using EventHub.Application.Common;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Admin;

namespace EventHub.Api.FunctionalTests;

/// <summary>
/// In-process stub for IIdentityAdminService used by the functional test host.
/// Removes the Microsoft Graph dependency so tests run without live Entra credentials.
/// </summary>
internal sealed class FakeIdentityAdminService : IIdentityAdminService
{
    /// <summary>A user ID that the fake treats as successfully reachable.</summary>
    public const string KnownUserId = "known-user-001";

    /// <summary>A user ID that the fake treats as not found (throws NotFoundException).</summary>
    public const string NotFoundUserId = "not-found-user";

    public Task<PagedResult<AdminUserDto>> GetUsersAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        var users = new List<AdminUserDto>
        {
            new(KnownUserId, "Known User", "known@example.com", IsOrganizer: false, IsAdmin: false),
        };
        return Task.FromResult(new PagedResult<AdminUserDto>(users, Page: page, PageSize: pageSize, TotalCount: 1));
    }

    public Task AssignOrganizerRoleAsync(
        string targetUserId, string actingAdminUserId, CancellationToken ct)
    {
        if (targetUserId == NotFoundUserId)
            throw new NotFoundException("User", targetUserId);

        return Task.CompletedTask;
    }

    public Task RemoveOrganizerRoleAsync(
        string targetUserId, string actingAdminUserId, CancellationToken ct)
    {
        if (targetUserId == NotFoundUserId)
            throw new NotFoundException("User", targetUserId);

        return Task.CompletedTask;
    }
}
