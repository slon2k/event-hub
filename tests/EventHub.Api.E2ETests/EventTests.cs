namespace EventHub.Api.E2ETests;

[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class EventTests(E2EFixture fixture)
{
    [Fact]
    public async Task CreateEvent_WithOrganizerToken_Returns201()
    {
        var (response, eventId) = await CreateDraftEventAsync();
        await CancelEventAsync(eventId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PublishEvent_Returns204()
    {
        var (_, eventId) = await CreateDraftEventAsync();

        var response = await fixture.OrganizerClient.PostAsync(
            $"/api/events/{eventId}/publish", null);

        await CancelEventAsync(eventId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_Returns200()
    {
        var (_, eventId) = await CreateDraftEventAsync();

        var response = await fixture.OrganizerClient.GetAsync($"/api/events/{eventId}");

        await CancelEventAsync(eventId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<(HttpResponseMessage Response, Guid EventId)> CreateDraftEventAsync()
    {
        var body = new
        {
            title = $"E2E Test {Guid.NewGuid():N}",
            description = "Created by E2E tests — safe to delete",
            dateTime = DateTimeOffset.UtcNow.AddDays(30),
            location = "E2E Test Venue",
            capacity = 10
        };

        var response = await fixture.OrganizerClient.PostAsJsonAsync("/api/events", body);
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        return (response, created!.Id);
    }

    private Task CancelEventAsync(Guid eventId) =>
        fixture.OrganizerClient.PostAsync($"/api/events/{eventId}/cancel", null);
}
