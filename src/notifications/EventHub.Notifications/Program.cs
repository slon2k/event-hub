using Azure.Communication.Email;
using EventHub.Infrastructure.Persistence;
using EventHub.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // ── Database ─────────────────────────────────────────────────────────────
        // Reuse the same SQL Server database as the API so the Functions project
        // reads OutboxMessages written by the API in the same transaction.
        services.AddDbContext<EventHubDbContext>(options =>
            options.UseSqlServer(
                config["ConnectionStrings:DefaultConnection"]
                    ?? throw new InvalidOperationException(
                        "Connection string 'DefaultConnection' is not configured."),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        // ── Service Bus ───────────────────────────────────────────────────────────
        services.AddAzureClients(builder =>
        {
            builder.AddServiceBusClient(
                config["ServiceBusConnectionString"]
                    ?? throw new InvalidOperationException(
                        "'ServiceBusConnectionString' is not configured."));
        });

        // ── Email sender ──────────────────────────────────────────────────────────
        // Set AcsEmail:UseStub=true in local.settings.json to log emails to the
        // console instead of sending via ACS — safe for local development.
        var useStub = string.Equals(config["AcsEmail:UseStub"], "true", StringComparison.OrdinalIgnoreCase);
        if (useStub)
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }
        else
        {
            services.AddSingleton(_ =>
                new EmailClient(
                    config["AcsEmail:ConnectionString"]
                        ?? throw new InvalidOperationException(
                            "'AcsEmail:ConnectionString' is not configured.")));

            services.AddSingleton<IEmailSender, AcsEmailSender>();
        }
    })
    .Build();

await host.RunAsync();
