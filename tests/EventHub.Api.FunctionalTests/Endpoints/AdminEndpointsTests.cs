using System.Text.Json;

namespace EventHub.Api.FunctionalTests.Endpoints;

[Collection("Api")]
public class AdminEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient AdminClient() =>
        factory.CreateDefaultClient().WithBearerToken(TokenHelper.AdminToken());

    private HttpClient OrganizerClient(string organizerId) =>
        factory.CreateDefaultClient().WithBearerToken(TokenHelper.OrganizerToken(organizerId));

    // ── GET /api/admin/events ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEvents_WithAdminToken_Returns200()
    {
        var response = await AdminClient().GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_WithAdminToken_ReturnsEventsFromAllOrganizers()
    {
        var org1 = "admin-test-org-001";
        var org2 = "admin-test-org-002";

        await OrganizerClient(org1).PostAsJsonAsync("/api/events", NewEventRequest("Admin Test Event Org1"));
        await OrganizerClient(org2).PostAsJsonAsync("/api/events", NewEventRequest("Admin Test Event Org2"));

        var response = await AdminClient().GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin Test Event Org1", body);
        Assert.Contains("Admin Test Event Org2", body);
    }

    [Fact]
    public async Task GetAllEvents_WithOrganizerToken_Returns403()
    {
        var response = await OrganizerClient("org-403-test").GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_Unauthenticated_Returns401()
    {
        var response = await factory.CreateDefaultClient().GetAsync("/api/admin/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static object NewEventRequest(string title) =>
        new
        {
            Title    = title,
            DateTime = DateTimeOffset.UtcNow.AddDays(14)
        };
}
