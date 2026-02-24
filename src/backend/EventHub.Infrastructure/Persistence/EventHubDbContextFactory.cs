using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EventHub.Infrastructure.Persistence;

/// <summary>
/// Used exclusively by the EF Core design-time tools (dotnet ef migrations ...).
/// Not part of the production DI composition.
///
/// Connection resolution order:
/// 1) --connection=<value> argument
/// 2) ConnectionStrings__DefaultConnection environment variable
/// 3) ConnectionStrings:DefaultConnection from API appsettings
/// </summary>
internal sealed class EventHubDbContextFactory : IDesignTimeDbContextFactory<EventHubDbContext>
{
    public EventHubDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            GetConnectionFromArgs(args)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? GetConnectionFromConfiguration()
            ?? throw new InvalidOperationException(
                "Unable to resolve design-time connection string. Provide --connection=<value>, set ConnectionStrings__DefaultConnection, or configure ConnectionStrings:DefaultConnection in API appsettings.");

        var options = new DbContextOptionsBuilder<EventHubDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new EventHubDbContext(options);
    }

    private static string? GetConnectionFromArgs(string[] args)
    {
        const string prefix = "--connection=";

        var inline = args
            .FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(inline))
        {
            return inline[prefix.Length..];
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--connection", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string? GetConnectionFromConfiguration()
    {
        var apiBasePath = ResolveApiBasePath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiBasePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection");
    }

    private static string ResolveApiBasePath()
    {
        var current = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(current, "src", "backend", "EventHub.Api"),
            Path.Combine(current, "..", "EventHub.Api"),
            Path.Combine(current, ".")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "appsettings.json")))
            {
                return fullPath;
            }
        }

        return current;
    }
}
