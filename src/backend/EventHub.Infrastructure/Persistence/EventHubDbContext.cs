using System.Text.Json;
using EventHub.Application.Abstractions;
using EventHub.Domain.Common;
using EventHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Infrastructure.Persistence;

public sealed class EventHubDbContext : DbContext, IApplicationDbContext
{
    private readonly IOutboxNotifier? _outboxNotifier;

    public EventHubDbContext(
        DbContextOptions<EventHubDbContext> options,
        IOutboxNotifier? outboxNotifier = null)
        : base(options)
    {
        _outboxNotifier = outboxNotifier;
    }

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
    /// After a successful save, sends a wake-up ping to the outbox-trigger queue so
    /// the Azure Function processes the outbox on demand instead of waiting for the
    /// fallback timer tick.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var hadDomainEvents = DispatchDomainEventsToOutbox();
        var result = await base.SaveChangesAsync(cancellationToken);

        if (hadDomainEvents && _outboxNotifier is not null)
            await _outboxNotifier.NotifyAsync(cancellationToken);

        return result;
    }

    /// <returns>True if at least one domain event was dispatched.</returns>
    private bool DispatchDomainEventsToOutbox()
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

        return aggregates.Count > 0;
    }
}
