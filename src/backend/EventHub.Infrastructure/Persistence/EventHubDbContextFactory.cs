using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventHub.Infrastructure.Persistence;

/// <summary>
/// Used exclusively by the EF Core design-time tools (dotnet ef migrations ...).
/// Not part of the production DI composition.
/// </summary>
internal sealed class EventHubDbContextFactory : IDesignTimeDbContextFactory<EventHubDbContext>
{
    public EventHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EventHubDbContext>()
            .UseSqlServer(
                "Server=.;Database=EventHub;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.EnableRetryOnFailure())
            .Options;

        return new EventHubDbContext(options);
    }
}
