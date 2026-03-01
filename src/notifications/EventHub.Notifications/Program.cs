using Azure.Communication.Email;
using Azure.Data.Tables;
using Azure.Identity;
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
                config["ConnectionStrings:DefaultConnection"] ?? string.Empty,
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        // ── Service Bus ───────────────────────────────────────────────────────────
        services.AddAzureClients(builder =>
        {
            builder.AddServiceBusClient(
                config["ServiceBusConnectionString"] ?? string.Empty);
        });

        // ── Email sender ──────────────────────────────────────────────────────────
        // Set AcsEmail:UseStub=true to write emails to Azure Table Storage instead
        // of sending via ACS. Safe for local development (Azurite) and the dev
        // environment in Azure. Rows are queryable in Storage Explorer / Portal.
        var useStub = string.Equals(config["AcsEmail:UseStub"], "true", StringComparison.OrdinalIgnoreCase);
        if (useStub)
        {
            // Re-use the AzureWebJobsStorage account: managed identity in Azure,
            // Azurite connection string locally (UseDevelopmentStorage=true).
            // IConfiguration converts __ → : for env vars, so the app setting
            // AzureWebJobsStorage__accountName must be read with a colon here.
            var storageAccountName = config["AzureWebJobsStorage:accountName"];
            var tableClient = !string.IsNullOrEmpty(storageAccountName)
                ? new TableServiceClient(
                    new Uri($"https://{storageAccountName}.table.core.windows.net"),
                    new ManagedIdentityCredential())
                : new TableServiceClient(
                    config["AzureWebJobsStorage"] ?? "UseDevelopmentStorage=true");

            services.AddSingleton(tableClient);
            services.AddSingleton<IEmailSender, TableStorageEmailSender>();
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
