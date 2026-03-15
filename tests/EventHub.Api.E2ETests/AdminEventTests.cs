namespace EventHub.Api.E2ETests;

[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class AdminEventTests(E2EFixture fixture)
{
    [Fact]
    public async Task GetAllEvents_WithAdminToken_Returns200()
    {
        var response = await fixture.AdminClient.GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_WithOrganizerToken_Returns403()
    {
        var response = await fixture.OrganizerClient.GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_Unauthenticated_Returns401()
    {
        var response = await fixture.AnonymousClient.GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
