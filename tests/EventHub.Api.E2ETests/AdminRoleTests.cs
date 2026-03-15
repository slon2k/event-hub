namespace EventHub.Api.E2ETests;

/// <summary>
/// E2E tests for organizer role assignment and removal.
///
/// Role lifecycle tests (Assign/Remove) require EVENTHUB_TEST_TARGET_USER_ID to be set
/// to a real user ID in the target Entra tenant. They are silently skipped when the
/// variable is absent so the suite can run in environments without Graph permissions.
///
/// Self-assign and auth rejection tests run unconditionally.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class AdminRoleTests(E2EFixture fixture)
{
    // ── Role lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns then removes the Organizer role, exercising idempotency at each step.
    /// Requires EVENTHUB_TEST_TARGET_USER_ID.
    /// </summary>
    [Fact]
    public async Task AssignAndRemoveOrganizerRole_Lifecycle_IsIdempotent()
    {
        if (string.IsNullOrEmpty(fixture.TargetUserId)) return;

        var url = $"/api/admin/users/{fixture.TargetUserId}/roles/organizer";

        var assign = await fixture.AdminClient.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var reassign = await fixture.AdminClient.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.NoContent, reassign.StatusCode);

        var remove = await fixture.AdminClient.DeleteAsync(url);
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var reremove = await fixture.AdminClient.DeleteAsync(url);
        Assert.Equal(HttpStatusCode.NoContent, reremove.StatusCode);
    }

    // ── Self-assign guard ─────────────────────────────────────────────────────

    [Fact]
    public async Task AssignOrganizerRole_SelfAssign_Returns403()
    {
        var adminUserId = E2EFixture.GetOidFromToken(fixture.AdminToken);

        var response = await fixture.AdminClient.PostAsync(
            $"/api/admin/users/{adminUserId}/roles/organizer", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveOrganizerRole_SelfRemove_Returns403()
    {
        var adminUserId = E2EFixture.GetOidFromToken(fixture.AdminToken);

        var response = await fixture.AdminClient.DeleteAsync(
            $"/api/admin/users/{adminUserId}/roles/organizer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Authorization rejections ──────────────────────────────────────────────

    [Fact]
    public async Task AssignOrganizerRole_WithOrganizerToken_Returns403()
    {
        var response = await fixture.OrganizerClient.PostAsync(
            "/api/admin/users/some-user-id/roles/organizer", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveOrganizerRole_WithOrganizerToken_Returns403()
    {
        var response = await fixture.OrganizerClient.DeleteAsync(
            "/api/admin/users/some-user-id/roles/organizer");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignOrganizerRole_UnknownUser_Returns404()
    {
        if (string.IsNullOrEmpty(fixture.TargetUserId)) return;

        var response = await fixture.AdminClient.PostAsync(
            "/api/admin/users/00000000-0000-0000-0000-000000000000/roles/organizer", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
