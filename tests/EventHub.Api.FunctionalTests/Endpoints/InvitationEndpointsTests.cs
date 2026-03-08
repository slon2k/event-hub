using System.Text.Json;
using EventHub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventHub.Api.FunctionalTests.Endpoints;

[Collection("Api")]
public class InvitationEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient Client(string organizerId) =>
        factory.CreateDefaultClient().WithBearerToken(TokenHelper.OrganizerToken(organizerId));

    // ── POST /api/events/{eventId}/invitations ────────────────────────────────

    [Fact]
    public async Task SendInvitation_PublishedEvent_Returns201()
    {
        var client = Client("org-inv-send-001");
        var eventId = await CreatePublishedEvent(client, "Invite Event 1");

        var response = await client.PostAsJsonAsync(
            $"/api/events/{eventId}/invitations",
            new { ParticipantEmail = "alice@example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SendInvitation_DuplicateEmail_Returns409()
    {
        var client = Client("org-inv-send-002");
        var eventId = await CreatePublishedEvent(client, "Invite Event 2");
        await client.PostAsJsonAsync($"/api/events/{eventId}/invitations",
            new { ParticipantEmail = "bob@example.com" });

        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/invitations",
            new { ParticipantEmail = "bob@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SendInvitation_DifferentOrganizer_Returns403()
    {
        var owner = Client("org-inv-send-003");
        var other = Client("org-inv-send-004");
        var eventId = await CreatePublishedEvent(owner, "Invite Event 3");

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/invitations",
            new { ParticipantEmail = "eve@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SendInvitation_InvalidEmail_Returns422()
    {
        var client = Client("org-inv-send-005");
        var eventId = await CreatePublishedEvent(client, "Invite Event 5");

        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/invitations",
            new { ParticipantEmail = "not-an-email" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── DELETE /api/events/{eventId}/invitations/{invitationId} ──────────────

    [Fact]
    public async Task CancelInvitation_PendingInvitation_Returns204()
    {
        var client = Client("org-inv-cancel-001");
        var eventId = await CreatePublishedEvent(client, "Cancel Inv Event 1");
        var invitationId = await SendInvitation(client, eventId, "carol@example.com");

        var response = await client.DeleteAsync(
            $"/api/events/{eventId}/invitations/{invitationId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CancelInvitation_DifferentOrganizer_Returns403()
    {
        var owner = Client("org-inv-cancel-002");
        var other = Client("org-inv-cancel-003");
        var eventId = await CreatePublishedEvent(owner, "Cancel Inv Event 2");
        var invitationId = await SendInvitation(owner, eventId, "dave@example.com");

        var response = await other.DeleteAsync(
            $"/api/events/{eventId}/invitations/{invitationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/invitations/respond (anonymous RSVP) ────────────────────────

    [Fact]
    public async Task RespondToInvitation_InvalidResponseValue_Returns422()
    {
        // No token required — endpoint is AllowAnonymous
        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = Guid.NewGuid(), RawToken = "any", Response = "maybe" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task RespondToInvitation_UnknownToken_Returns400()
    {
        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = Guid.NewGuid(), RawToken = "non-existent-token", Response = "Accept" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RespondToInvitation_ValidToken_Accept_Returns204()
    {
        var client = Client("org-rsvp-accept-001");
        var eventId = await CreatePublishedEvent(client, "RSVP Accept Event");
        var invitationId = await SendInvitation(client, eventId, "rsvp-accept@example.com");
        var rawToken = await GetRsvpTokenFromOutboxAsync(invitationId);

        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = rawToken, Response = "Accept" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RespondToInvitation_ValidToken_Decline_Returns204()
    {
        var client = Client("org-rsvp-decline-001");
        var eventId = await CreatePublishedEvent(client, "RSVP Decline Event");
        var invitationId = await SendInvitation(client, eventId, "rsvp-decline@example.com");
        var rawToken = await GetRsvpTokenFromOutboxAsync(invitationId);

        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = rawToken, Response = "Decline" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RespondToInvitation_AlreadyUsedToken_Returns409()
    {
        var client = Client("org-rsvp-used-001");
        var eventId = await CreatePublishedEvent(client, "RSVP Used Token Event");
        var invitationId = await SendInvitation(client, eventId, "rsvp-used@example.com");
        var rawToken = await GetRsvpTokenFromOutboxAsync(invitationId);

        await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = rawToken, Response = "Accept" });

        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = rawToken, Response = "Accept" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RespondToInvitation_ExpiredToken_Returns400()
    {
        var client = Client("org-rsvp-expired-001");
        const string email = "rsvp-expired@example.com";
        var eventId = await CreatePublishedEvent(client, "RSVP Expired Token Event");
        var invitationId = await SendInvitation(client, eventId, email);

        var tokenService = new RsvpTokenService(ApiFactory.TestHmacKeyBase64);
        var (expiredToken, _) = tokenService.Generate(invitationId, email, DateTimeOffset.UtcNow.AddHours(-1));

        var response = await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = expiredToken, Response = "Accept" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST /api/events/{eventId}/invitations/{invitationId}/reissue ─────────

    [Fact]
    public async Task ReissueInvitationToken_PendingInvitation_Returns204()
    {
        var client = Client("org-reissue-001");
        var eventId = await CreatePublishedEvent(client, "Reissue Event 1");
        var invitationId = await SendInvitation(client, eventId, "reissue-pending@example.com");

        var response = await client.PostAsync(
            $"/api/events/{eventId}/invitations/{invitationId}/reissue", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ReissueInvitationToken_DifferentOrganizer_Returns403()
    {
        var owner = Client("org-reissue-002");
        var other = Client("org-reissue-003");
        var eventId = await CreatePublishedEvent(owner, "Reissue Event 2");
        var invitationId = await SendInvitation(owner, eventId, "reissue-forbidden@example.com");

        var response = await other.PostAsync(
            $"/api/events/{eventId}/invitations/{invitationId}/reissue", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReissueInvitationToken_AcceptedInvitation_Returns409()
    {
        var client = Client("org-reissue-004");
        const string email = "reissue-accepted@example.com";
        var eventId = await CreatePublishedEvent(client, "Reissue Event 3");
        var invitationId = await SendInvitation(client, eventId, email);
        var rawToken = await GetRsvpTokenFromOutboxAsync(invitationId);

        await factory.CreateDefaultClient().PostAsJsonAsync(
            "/api/invitations/respond",
            new { InvitationId = invitationId, RawToken = rawToken, Response = "Accept" });

        var response = await client.PostAsync(
            $"/api/events/{eventId}/invitations/{invitationId}/reissue", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Health check ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Healthz_Returns200()
    {
        var response = await factory.CreateDefaultClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GetRsvpTokenFromOutboxAsync(Guid invitationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventHubDbContext>();
        var outbox = await db.OutboxMessages
            .Where(m => m.Payload.Contains(invitationId.ToString()))
            .OrderByDescending(m => m.CreatedAt)
            .FirstAsync();
        using var doc = JsonDocument.Parse(outbox.Payload);
        return doc.RootElement.GetProperty("RsvpToken").GetString()!;
    }

    private static async Task<Guid> CreatePublishedEvent(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/api/events", new
        {
            Title = title,
            Description = (string?)null,
            DateTime = DateTimeOffset.UtcNow.AddDays(30),
            Location = (string?)null,
            Capacity = (int?)null
        });
        var body = await created.Content.ReadAsStringAsync();
        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
        await client.PostAsync($"/api/events/{id}/publish", null);
        return id;
    }

    private static async Task<Guid> SendInvitation(HttpClient client, Guid eventId, string email)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/events/{eventId}/invitations", new { ParticipantEmail = email });
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
    }
}
