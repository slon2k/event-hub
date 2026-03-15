using Azure.Messaging.ServiceBus;
using EventHub.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHub.Infrastructure.Messaging;

/// <summary>
/// Sends a lightweight "ping" to the <c>outbox-trigger</c> Service Bus queue
/// immediately after domain events are written to the outbox table.
/// The Azure Function wakes via <c>ServiceBusTrigger</c> and processes the
/// outbox on demand, allowing the DB to auto-pause between real activity.
/// </summary>
/// <remarks>
/// Failures are swallowed and logged as warnings — a notifier failure must
/// never roll back the domain transaction. The fallback timer in the Function
/// App guarantees eventual processing even if the ping is lost.
/// </remarks>
internal sealed class ServiceBusOutboxNotifier(
    ServiceBusClient serviceBusClient,
    IConfiguration configuration,
    ILogger<ServiceBusOutboxNotifier> logger) : IOutboxNotifier
{
    public async Task NotifyAsync(CancellationToken cancellationToken = default)
    {
        var queueName = configuration["ServiceBus:OutboxTriggerQueueName"] ?? "outbox-trigger";

        try
        {
            await using var sender = serviceBusClient.CreateSender(queueName);

            var message = new ServiceBusMessage("ping")
            {
                // Expire quickly — if the function didn't pick it up within 5 min
                // the fallback timer will process the outbox anyway.
                TimeToLive = TimeSpan.FromMinutes(5)
            };

            await sender.SendMessageAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Outbox wake-up ping failed (queue: {QueueName}). " +
                "The fallback timer trigger will process the outbox eventually.",
                queueName);
        }
    }
}
