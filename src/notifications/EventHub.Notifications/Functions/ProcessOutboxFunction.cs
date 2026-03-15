using Azure.Messaging.ServiceBus;
using EventHub.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Functions;

/// <summary>
/// Processes <c>OutboxMessages</c> and publishes them to the Azure Service Bus
/// <c>notifications</c> topic using two complementary triggers:
/// <list type="bullet">
///   <item>
///     <term>On-demand (<see cref="RunOnDemand"/>)</term>
///     <description>
///       Fires instantly when the API sends a wake-up ping to the
///       <c>outbox-trigger</c> queue after writing a domain event.
///       This eliminates constant DB polling and allows the free-tier DB to
///       auto-pause between real user activity.
///     </description>
///   </item>
///   <item>
///     <term>Fallback (<see cref="RunFallback"/>)</term>
///     <description>
///       Fires according to the schedule to catch any pings lost during API restarts or
///       transient Service Bus errors.
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class ProcessOutboxFunction(
    EventHubDbContext dbContext,
    ServiceBusClient serviceBusClient,
    IConfiguration configuration,
    ILogger<ProcessOutboxFunction> logger)
{
    private const int BatchSize = 50;

    /// <summary>
    /// On-demand trigger: fired by the API wake-up ping dropped into the
    /// <c>outbox-trigger</c> queue immediately after a domain save.
    /// </summary>
    [Function("ProcessOutboxOnDemand")]
    public async Task RunOnDemand(
        [ServiceBusTrigger(
            "%OutboxTriggerQueueName%",
            Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage _,
        CancellationToken cancellationToken)
        => await ProcessAsync(cancellationToken);

    /// <summary>
    /// Fallback timer: fires according to the schedule to process any outbox messages
    /// whose wake-up ping was lost (e.g. API crash before sending the ping).
    /// The long interval lets the DB auto-pause between real activity.
    /// </summary>
    [Function("ProcessOutboxFallback")]
    public async Task RunFallback(
        [TimerTrigger("%OutboxTimerCronExpression%")] TimerInfo _,
        CancellationToken cancellationToken)
        => await ProcessAsync(cancellationToken);

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var topicName = configuration["ServiceBusTopicName"] ?? "notifications";

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
