using EventHub.Domain.Entities;
using EventHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Api;

/// <summary>
/// Populates the local development database with a fixed set of sample data.
/// Invoked via the <c>--seed</c> CLI argument; only runs in Development.
/// Idempotent: skips if seed events already exist for the seed organizer.
/// </summary>
internal static class DataSeeder
{
    // Stable GUID used as the seed organizer's oid claim.
    // Generate a matching local token with:
    //   dotnet user-jwts create --project src/backend/EventHub.Api --role Organizer --claim "oid=a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    private const string OrganizerId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    public static async Task RunAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventHubDbContext>();

        // Ensure the DB exists and all migrations are applied before seeding.
        await db.Database.MigrateAsync();

        // Idempotency: bail out if seed data is already present.
        if (await db.Events.AnyAsync(e => e.OrganizerId == OrganizerId))
        {
            Console.WriteLine("[seed] Seed data already present — skipping.");
            return;
        }

        Console.WriteLine("[seed] Seeding development data...");

        var now = DateTimeOffset.UtcNow;

        // ── Events ────────────────────────────────────────────────────────────

        var draftEvent = Event.Create(
            "Dev Draft Event",
            "A draft event for local development testing.",
            now.AddDays(30),
            "Room A",
            20,
            OrganizerId);

        var publishedEvent = Event.Create(
            "Dev Published Event",
            "A published event with sample invitations.",
            now.AddDays(14),
            "Main Hall",
            50,
            OrganizerId);
        publishedEvent.Publish();

        var cancelledEvent = Event.Create(
            "Dev Cancelled Event",
            "A cancelled event for local development testing.",
            now.AddDays(7),
            "Room B",
            10,
            OrganizerId);
        cancelledEvent.Publish();
        cancelledEvent.Cancel();

        db.Events.Add(draftEvent);
        db.Events.Add(publishedEvent);
        db.Events.Add(cancelledEvent);

        // ── Invitations (on the Published event) ──────────────────────────────

        var tokenExpiry = now.AddHours(72);

        var acceptedInv = publishedEvent.AddInvitation(
            "accepted@seed.example",
            "seed-raw-accepted",
            HashRaw("seed-raw-accepted"),
            tokenExpiry);

        var declinedInv = publishedEvent.AddInvitation(
            "declined@seed.example",
            "seed-raw-declined",
            HashRaw("seed-raw-declined"),
            tokenExpiry);

        publishedEvent.AddInvitation(
            "pending@seed.example",
            "seed-raw-pending",
            HashRaw("seed-raw-pending"),
            tokenExpiry);

        publishedEvent.AcceptInvitation(acceptedInv.Id);
        publishedEvent.DeclineInvitation(declinedInv.Id);

        await db.SaveChangesAsync();

        Console.WriteLine($"[seed] Created 3 events for organizer '{OrganizerId}':");
        Console.WriteLine($"[seed]   • {draftEvent.Title} (Draft)");
        Console.WriteLine($"[seed]   • {publishedEvent.Title} (Published)");
        Console.WriteLine($"[seed]   • {cancelledEvent.Title} (Cancelled)");
        Console.WriteLine($"[seed] Created 3 invitations on '{publishedEvent.Title}':");
        Console.WriteLine("[seed]   • accepted@seed.example (Accepted)");
        Console.WriteLine("[seed]   • declined@seed.example (Declined)");
        Console.WriteLine("[seed]   • pending@seed.example  (Pending)");
        Console.WriteLine("[seed] Done.");
    }

    /// <summary>
    /// Returns a deterministic base-64 SHA-256 hash of the raw token string.
    /// Seed invitations use placeholder tokens — they produce valid DB rows but
    /// the magic links won't work (by design; this is dev-only seed data).
    /// </summary>
    private static string HashRaw(string raw) =>
        Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(raw)));
}
