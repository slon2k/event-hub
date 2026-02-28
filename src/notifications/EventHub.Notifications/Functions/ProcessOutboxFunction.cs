using Azure.Messaging.ServiceBus;
using EventHub.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Functions;

/// <summary>
/// Polls the <c>OutboxMessages</c> table every 10 seconds and publishes unpublished
/// entries to the Azure Service Bus <c>notifications</c> topic.
/// Marks each message as published immediately after a successful send so that
/// partial failures leave unprocessed rows available for the next timer tick.
/// </summary>
public sealed class ProcessOutboxFunction(
    EventHubDbContext dbContext,
    ServiceBusClient serviceBusClient,
    IConfiguration configuration,
    ILogger<ProcessOutboxFunction> logger)
{
    private const int BatchSize = 50;

    [Function("ProcessOutboxFunction")]
    public async Task Run(
        [TimerTrigger("%Outbox__TimerCronExpression%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        var topicName = configuration["ServiceBus__TopicName"] ?? "notifications";

        var messages = await dbContext.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            logger.LogDebug("No unpublished outbox messages found.");
            return;
        }

        logger.LogInformation("Processing {Count} outbox message(s).", messages.Count);

        await using var sender = serviceBusClient.CreateSender(topicName);

        foreach (var outbox in messages)
        {
            try
            {
                var sbMessage = new ServiceBusMessage(BinaryData.FromString(outbox.Payload))
                {
                    // Subject carries the full domain event type name so SendEmailFunction
                    // can route without deserializing the body first.
                    Subject = outbox.Type,
                    // MessageId = outbox Id enables Service Bus duplicate detection:
                    // if the function crashes after SendMessageAsync but before SaveChangesAsync,
                    // the next run republishes the same Id and Service Bus silently discards it.
                    MessageId = outbox.Id.ToString(),
                    ContentType = "application/json"
                };

                await sender.SendMessageAsync(sbMessage, cancellationToken);
                outbox.MarkPublished();

                // Persist PublishedAt immediately after each successful send.
                // This minimises the duplicate-publish window: only messages published
                // after the last SaveChangesAsync will be re-sent if the process crashes.
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Published outbox message {Id} ({Type}).", outbox.Id, outbox.Type);
            }
            catch (Exception ex)
            {
                outbox.MarkFailed(ex.Message);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(
                    ex, "Failed to publish outbox message {Id} ({Type}).", outbox.Id, outbox.Type);
            }
        }
    }
}
