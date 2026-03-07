using System.Text.Json;

namespace EventHub.Api.FunctionalTests.Endpoints;

[Collection("Api")]
public class EventEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient Client(string organizerId) =>
        factory.CreateDefaultClient().WithBearerToken(TokenHelper.OrganizerToken(organizerId));

    // ── GET /api/events ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyEvents_Unauthenticated_Returns401()
    {
        var response = await factory.CreateDefaultClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyEvents_Authenticated_Returns200WithOnlyOwnEvents()
    {
        var ownerId   = "org-get-events-001";
        var otherId   = "org-get-events-002";
        var client    = Client(ownerId);
        var otherClient = Client(otherId);

        // Create one event for this organizer and one for another
        await client.PostAsJsonAsync("/api/events", NewEventRequest("Owner event"));
        await otherClient.PostAsJsonAsync("/api/events", NewEventRequest("Other event"));

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Owner event", body);
        Assert.DoesNotContain("Other event", body);
    }

    // ── POST /api/events ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_ValidPayload_Returns201WithLocationHeader()
    {
        var response = await Client("org-create-001").PostAsJsonAsync(
            "/api/events", NewEventRequest("Create Test Event"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateEvent_MissingTitle_Returns422()
    {
        var response = await Client("org-create-002").PostAsJsonAsync(
            "/api/events", new { Title = "", DateTime = DateTimeOffset.UtcNow.AddDays(7) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Unauthenticated_Returns401()
    {
        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/events", NewEventRequest("Unauth Event"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/events/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetEventById_ExistingEvent_Returns200()
    {
        var client = Client("org-get-by-id-001");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Get By Id Event"));
        var id = await ExtractId(created);

        var response = await client.GetAsync($"/api/events/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Get By Id Event", body);
    }

    [Fact]
    public async Task GetEventById_NonExistentId_Returns404()
    {
        var response = await Client("org-get-by-id-002").GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /api/events/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_ValidPayload_Returns204()
    {
        var client = Client("org-update-001");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Before Update"));
        var id = await ExtractId(created);

        var response = await client.PutAsJsonAsync($"/api/events/{id}",
            NewEventRequest("After Update"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_DifferentOrganizer_Returns403()
    {
        var owner = Client("org-update-002");
        var other = Client("org-update-003");
        var created = await owner.PostAsJsonAsync("/api/events", NewEventRequest("Owned Event"));
        var id = await ExtractId(created);

        var response = await other.PutAsJsonAsync($"/api/events/{id}",
            NewEventRequest("Hijack Attempt"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/events/{id}/publish ─────────────────────────────────────────

    [Fact]
    public async Task PublishEvent_Draft_Returns204()
    {
        var client = Client("org-publish-001");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Publish Me"));
        var id = await ExtractId(created);

        var response = await client.PostAsync($"/api/events/{id}/publish", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PublishEvent_AlreadyPublished_Returns409()
    {
        var client = Client("org-publish-002");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Double Publish"));
        var id = await ExtractId(created);
        await client.PostAsync($"/api/events/{id}/publish", null);

        var response = await client.PostAsync($"/api/events/{id}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── POST /api/events/{id}/cancel ──────────────────────────────────────────

    [Fact]
    public async Task CancelEvent_PublishedEvent_Returns204()
    {
        var client = Client("org-cancel-001");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Cancel Me"));
        var id = await ExtractId(created);
        await client.PostAsync($"/api/events/{id}/publish", null);

        var response = await client.PostAsync($"/api/events/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CancelEvent_AlreadyCancelled_Returns409()
    {
        var client = Client("org-cancel-002");
        var created = await client.PostAsJsonAsync("/api/events", NewEventRequest("Cancel Twice"));
        var id = await ExtractId(created);
        await client.PostAsync($"/api/events/{id}/publish", null);
        await client.PostAsync($"/api/events/{id}/cancel", null);

        var response = await client.PostAsync($"/api/events/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object NewEventRequest(string title) => new
    {
        Title = title,
        Description = (string?)null,
        DateTime = DateTimeOffset.UtcNow.AddDays(30),
        Location = (string?)null,
        Capacity = (int?)null
    };

    private static async Task<Guid> ExtractId(HttpResponseMessage created)
    {
        var body = await created.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }
}
