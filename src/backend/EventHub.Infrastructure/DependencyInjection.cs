using Azure.Messaging.ServiceBus;
using EventHub.Application.Abstractions;
using EventHub.Domain.Services;
using EventHub.Infrastructure.Messaging;
using EventHub.Infrastructure.Persistence;
using EventHub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<EventHubDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<IApplicationDbContext>(
            provider => provider.GetRequiredService<EventHubDbContext>());

        var rsvpKey = configuration["RsvpToken:HmacKey"]
            ?? throw new InvalidOperationException(
                "Configuration key 'RsvpToken:HmacKey' is not set.");

        services.AddSingleton<IRsvpTokenService>(
            new RsvpTokenService(rsvpKey));

        // ── Outbox wake-up notifier ──────────────────────────────────────────────
        // Optional: only registered when ServiceBusConnectionString is configured.
        // When absent (e.g. local dev without Service Bus), the DbContext simply
        // skips the ping and relies on the fallback timer in the Function App.
        var sbConnectionString = configuration["ServiceBusConnectionString"];
        if (!string.IsNullOrEmpty(sbConnectionString))
        {
            services.AddSingleton(new ServiceBusClient(sbConnectionString));
            services.AddSingleton<IOutboxNotifier, ServiceBusOutboxNotifier>();
        }

        return services;
    }
}
