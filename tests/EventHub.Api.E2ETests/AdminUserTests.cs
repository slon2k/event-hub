namespace EventHub.Api.E2ETests;

[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class AdminUserTests(E2EFixture fixture)
{
    [Fact]
    public async Task GetUsers_WithAdminToken_Returns200_WithPagedResultShape()
    {
        var response = await fixture.AdminClient.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<PagedResultResponse<AdminUserResponse>>();

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.Page >= 1);
        Assert.True(result.PageSize >= 1);
        Assert.True(result.TotalCount >= 0);
    }

    [Fact]
    public async Task GetUsers_WithOrganizerToken_Returns403()
    {
        var response = await fixture.OrganizerClient.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_Unauthenticated_Returns401()
    {
        var response = await fixture.AnonymousClient.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
