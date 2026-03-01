using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EventHub.Domain.Events;
using EventHub.Notifications.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Functions;

/// <summary>
/// Triggered by messages arriving on the <c>notifications</c> topic / <c>email</c> subscription.
/// Routes each message to the appropriate email template based on the <c>Subject</c> header
/// (set by <see cref="ProcessOutboxFunction"/> to the domain event's full type name).
/// </summary>
public sealed class SendEmailFunction(
    IEmailSender emailSender,
    ILogger<SendEmailFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Function("SendEmailFunction")]
    public async Task Run(
        [ServiceBusTrigger(
            "%ServiceBusTopicName%",
            "%ServiceBusSubscriptionName%",
            Connection = "ServiceBusConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var type = message.Subject ?? string.Empty;
        var body = message.Body.ToString();

        logger.LogInformation(
            "Processing Service Bus message {MessageId} of type {Type}.", message.MessageId, type);

        if (type.EndsWith(nameof(InvitationSent), StringComparison.Ordinal))
        {
            var evt = JsonSerializer.Deserialize<InvitationSent>(body, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize {nameof(InvitationSent)} payload.");

            await emailSender.SendInvitationAsync(evt, cancellationToken);
        }
        else if (type.EndsWith(nameof(EventCancelled), StringComparison.Ordinal))
        {
            var evt = JsonSerializer.Deserialize<EventCancelled>(body, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize {nameof(EventCancelled)} payload.");

            await emailSender.SendCancellationAsync(evt, cancellationToken);
        }
        else if (type.EndsWith(nameof(InvitationResponded), StringComparison.Ordinal))
        {
            // No email sent for RSVP responses in v1 — the organizer can check
            // invitation status via GET /api/events/{eventId}/invitations.
            logger.LogInformation(
                "InvitationResponded received for message {MessageId} — no email required in v1.",
                message.MessageId);
        }
        else
        {
            // Unknown type: log and complete (do not dead-letter) to avoid poison messages
            // from legitimate future event types deployed before this function is updated.
            logger.LogWarning(
                "Unrecognized message type '{Type}' (MessageId: {MessageId}). Completing without processing.",
                type, message.MessageId);
        }
    }
}
