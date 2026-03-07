namespace EventHub.Infrastructure.IntegrationTests.Persistence;

[Collection("Database")]
public class EventPersistenceTests(DatabaseFixture db)
{
    private static (string Raw, string Hash, DateTimeOffset Expires) FakeToken(int seed = 1) =>
        ($"raw-token-{seed}", new string((char)('a' + seed - 1), 64), DateTimeOffset.UtcNow.AddHours(72));

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CanSaveAndReload_Event_PreservesAllProperties()
    {
        var futureDate = DateTimeOffset.UtcNow.AddDays(14);
        var ev = Event.Create("Integration Conf", "Annual conf", futureDate, "Berlin", 100, "org-rt-001");

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var loaded = await readCtx.Events.FindAsync(ev.Id);

        Assert.NotNull(loaded);
        Assert.Equal(ev.Id, loaded.Id);
        Assert.Equal("Integration Conf", loaded.Title);
        Assert.Equal("Annual conf", loaded.Description);
        Assert.Equal(futureDate, loaded.DateTime);
        Assert.Equal("Berlin", loaded.Location);
        Assert.Equal(100, loaded.Capacity);
        Assert.Equal("org-rt-001", loaded.OrganizerId);
        Assert.Equal(EventStatus.Draft, loaded.Status);
    }

    // ── ValueGeneratedNever ───────────────────────────────────────────────────

    [Fact]
    public async Task ClientAssignedId_IsPreservedByDatabase()
    {
        var ev = Event.Create("Id Preservation", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-id-001");
        var expectedId = ev.Id;

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var loaded = await readCtx.Events.FindAsync(expectedId);

        Assert.NotNull(loaded);
        Assert.Equal(expectedId, loaded.Id);
    }

    // ── Cascade delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_CascadeDeletes_Invitations()
    {
        var ev = Event.Create("Cascade Test", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-cd-001");
        ev.Publish();
        ev.ClearDomainEvents();
        var (raw, hash, expires) = FakeToken();
        ev.AddInvitation("cascade@example.com", raw, hash, expires);
        ev.ClearDomainEvents();
        var invitationId = ev.Invitations.First().Id;

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            var toDelete = await ctx.Events.FindAsync(ev.Id);
            ctx.Events.Remove(toDelete!);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = db.CreateContext();
        var deletedEvent = await readCtx.Events.FindAsync(ev.Id);
        var orphanedInvitation = await readCtx.Set<Invitation>().FindAsync(invitationId);

        Assert.Null(deletedEvent);
        Assert.Null(orphanedInvitation);
    }

    // ── Filtered unique index ─────────────────────────────────────────────────

    [Fact]
    public async Task FilteredUniqueIndex_AllowsReinviteAfterCancellation()
    {
        // Arrange: create event, invite alice, cancel that invitation — all saved
        var ev = Event.Create("Reinvite Test", null, DateTimeOffset.UtcNow.AddDays(1), null, null, "org-fi-001");
        ev.Publish();
        var (raw, hash, expires) = FakeToken();
        ev.AddInvitation("reinvite-alice@example.com", raw, hash, expires);
        var firstId = ev.Invitations.First().Id;
        ev.CancelInvitation(firstId);
        ev.ClearDomainEvents();

        await using (var ctx = db.CreateContext())
        {
            ctx.Events.Add(ev);
            await ctx.SaveChangesAsync();
        }

        // Act: re-invite same email in a fresh context — must not throw
        await using (var ctx = db.CreateContext())
        {
            var (raw2, hash2, expires2) = FakeToken(2);
            var loaded = await ctx.Events.Include(e => e.Invitations).FirstAsync(e => e.Id == ev.Id);
            loaded.AddInvitation("reinvite-alice@example.com", raw2, hash2, expires2);
            loaded.ClearDomainEvents();
            await ctx.SaveChangesAsync();
        }

        // Assert: both rows exist — the Cancelled one and the new Pending one
        await using var readCtx = db.CreateContext();
        var final = await readCtx.Events.Include(e => e.Invitations).FirstAsync(e => e.Id == ev.Id);
        Assert.Equal(2, final.Invitations.Count);
        Assert.Single(final.Invitations, i => i.Status == InvitationStatus.Cancelled);
        Assert.Single(final.Invitations, i => i.Status == InvitationStatus.Pending);
    }
}
