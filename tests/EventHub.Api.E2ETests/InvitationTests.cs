namespace EventHub.Api.E2ETests;

/// <summary>
/// E2E tests for invitation endpoints.
/// Each test creates its own published event via IAsyncLifetime (xUnit creates a
/// new class instance per test method, so each fact gets independent setup/teardown).
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public sealed class InvitationTests(E2EFixture fixture) : IAsyncLifetime
{
    private Guid _eventId;

    public async Task InitializeAsync()
    {
        var body = new
        {
            title = $"E2E Inv {Guid.NewGuid():N}",
            description = "Created by E2E tests — safe to delete",
            dateTime = DateTimeOffset.UtcNow.AddDays(30),
            location = "E2E Test Venue",
            capacity = 20
        };

        var createResp = await fixture.OrganizerClient.PostAsJsonAsync("/api/events", body);
        var created = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        _eventId = created!.Id;

        // Publish so invitations can be sent.
        await fixture.OrganizerClient.PostAsync($"/api/events/{_eventId}/publish", null);
    }

    public Task DisposeAsync() =>
        fixture.OrganizerClient.PostAsync($"/api/events/{_eventId}/cancel", null);

    [Fact]
    public async Task SendInvitation_WithOrganizerToken_Returns201()
    {
        var body = new { participantEmail = $"e2e-{Guid.NewGuid():N}@example.com" };

        var response = await fixture.OrganizerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/invitations", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CancelInvitation_Returns204()
    {
        var sendBody = new { participantEmail = $"e2e-{Guid.NewGuid():N}@example.com" };
        var sendResp = await fixture.OrganizerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/invitations", sendBody);
        var created = await sendResp.Content.ReadFromJsonAsync<IdResponse>();

        var response = await fixture.OrganizerClient.DeleteAsync(
            $"/api/events/{_eventId}/invitations/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact(Skip = "Requires RSVP token from EmailOutbox table storage — not testable via HTTP alone.")]
    public Task RespondAccept_Returns204() => Task.CompletedTask;

    [Fact(Skip = "Requires RSVP token from EmailOutbox table storage — not testable via HTTP alone.")]
    public Task RespondDecline_Returns204() => Task.CompletedTask;
}
