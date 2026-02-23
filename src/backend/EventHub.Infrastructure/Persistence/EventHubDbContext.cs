using System.Text.Json;
using EventHub.Application.Abstractions;
using EventHub.Domain.Common;
using EventHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Infrastructure.Persistence;

public sealed class EventHubDbContext : DbContext, IApplicationDbContext
{
    public EventHubDbContext(DbContextOptions<EventHubDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventHubDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Before committing, converts all pending domain events on tracked aggregates
    /// into OutboxMessage rows — written in the same transaction as the domain change.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DispatchDomainEventsToOutbox();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void DispatchDomainEventsToOutbox()
    {
        var aggregates = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var outbox = OutboxMessage.Create(
                    type: domainEvent.GetType().FullName!,
                    payload: JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

                OutboxMessages.Add(outbox);
            }

            aggregate.ClearDomainEvents();
        }
    }
}
