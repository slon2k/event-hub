using EventHub.Domain.Common;

namespace EventHub.Infrastructure.IntegrationTests.Persistence;

[Collection("Database")]
public class OutboxPersistenceTests(DatabaseFixture db)
{
    private static (string Raw, string Hash, DateTimeOffset Expires) FakeToken(int seed = 1) =>
        ($"raw-token-{seed}", new string((char)('a' + seed - 1), 64), DateTimeOffset.UtcNow.AddHours(72));

    private static Event CreatePublishedEvent(string organizerId)
    {
        var ev = Event.Create("Outbox Test", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
        ev.Publish();
        ev.ClearDomainEvents();
        return ev;
    }

    // ── Domain events → OutboxMessage dispatch ────────────────────────────────

    [Fact]
    public async Task AddInvitation_SaveChanges_WritesInvitationSentOutboxRow()
    {
        var ev = CreatePublishedEvent("org-ob-001");
        var (raw, hash, expires) = FakeToken();
        ev.AddInvitation("outbox-alice@example.com", raw, hash, expires);
        // ev has one InvitationSent domain event

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var outboxRow = await readCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type.EndsWith("InvitationSent") && m.Payload.Contains("outbox-alice@example.com"));

        Assert.NotNull(outboxRow);
        Assert.Null(outboxRow.PublishedAt);
        Assert.Equal(0, outboxRow.RetryCount);
    }

    [Fact]
    public async Task CancelEvent_SaveChanges_WritesEventCancelledOutboxRow()
    {
        var ev = CreatePublishedEvent("org-ob-002");
        ev.Cancel();
        // ev has one EventCancelled domain event

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var outboxRow = await readCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type.EndsWith("EventCancelled") && m.Payload.Contains(ev.Id.ToString()));

        Assert.NotNull(outboxRow);
    }

    [Fact]
    public async Task MultipleInvitations_SaveChanges_WritesOneOutboxRowPerInvitation()
    {
        var ev = CreatePublishedEvent("org-ob-003");
        var (raw, hash, expires) = FakeToken();
        var (raw2, hash2, expires2) = FakeToken(2);
        ev.AddInvitation("multi-alice@example.com", raw, hash, expires);
        ev.AddInvitation("multi-bob@example.com", raw2, hash2, expires2);

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var count = await readCtx.OutboxMessages
            .CountAsync(m => m.Type.EndsWith("InvitationSent") &&
                             (m.Payload.Contains("multi-alice@example.com") ||
                              m.Payload.Contains("multi-bob@example.com")));

        Assert.Equal(2, count);
    }

    // ── OutboxMessage persistence ─────────────────────────────────────────────

    [Fact]
    public async Task OutboxMessage_MarkPublished_PersistsPublishedAt()
    {
        var message = OutboxMessage.Create("TestEvent", "{\"key\":\"mark-published\"}");

        await using (var ctx = db.CreateContext())
        {
            ctx.OutboxMessages.Add(message);
            await ctx.SaveChangesAsync();
        }

        var before = DateTimeOffset.UtcNow;

        await using (var ctx = db.CreateContext())
        {
            var loaded = await ctx.OutboxMessages.FindAsync(message.Id);
            loaded!.MarkPublished();
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var reloaded = await readCtx.OutboxMessages.FindAsync(message.Id);

        Assert.NotNull(reloaded!.PublishedAt);
        Assert.True(reloaded.PublishedAt >= before);
        Assert.Null(reloaded.Error);
    }

    [Fact]
    public async Task OutboxMessage_MarkFailed_PersistsRetryCountAndError()
    {
        var message = OutboxMessage.Create("TestEvent", "{\"key\":\"mark-failed\"}");

        await using (var ctx = db.CreateContext())
        {
            ctx.OutboxMessages.Add(message);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            var loaded = await ctx.OutboxMessages.FindAsync(message.Id);
            loaded!.MarkFailed("connection timeout");
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var reloaded = await readCtx.OutboxMessages.FindAsync(message.Id);

        Assert.Equal(1, reloaded!.RetryCount);
        Assert.Equal("connection timeout", reloaded.Error);
        Assert.Null(reloaded.PublishedAt);
    }
}
