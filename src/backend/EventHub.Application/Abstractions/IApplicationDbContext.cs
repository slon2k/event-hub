using EventHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Abstractions;

/// <summary>
/// Abstraction over the EF Core DbContext exposed to Application layer handlers.
/// Keeps Application decoupled from the concrete Infrastructure implementation.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Event> Events { get; }
    DbSet<ApplicationUser> ApplicationUsers { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
