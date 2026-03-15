using Azure;
using Azure.Communication.Email;
using EventHub.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Services;

/// <summary>
/// Sends emails via Azure Communication Services Email.
/// In local development, prefer <see cref="ConsoleEmailSender"/> by setting
/// <c>AcsEmail:UseStub=true</c> in <c>local.settings.json</c>.
/// </summary>
public sealed class AcsEmailSender(
    EmailClient emailClient,
    IConfiguration configuration,
    ILogger<AcsEmailSender> logger) : IEmailSender
{
    private string SenderAddress =>
        configuration["AcsEmail:SenderAddress"] ?? "noreply@eventhub.example.com";

    private string AppBaseUrl =>
        configuration["App:BaseUrl"] ?? "https://eventhub.example.com";

    public async Task SendInvitationAsync(InvitationSent evt, CancellationToken cancellationToken = default)
    {
        var acceptUrl  = $"{AppBaseUrl}/rsvp/{evt.InvitationId}?token={Uri.EscapeDataString(evt.RsvpToken)}&response=Accept";
        var declineUrl = $"{AppBaseUrl}/rsvp/{evt.InvitationId}?token={Uri.EscapeDataString(evt.RsvpToken)}&response=Decline";

        var locationLine = evt.EventLocation is not null
            ? $"<p><strong>Location:</strong> {evt.EventLocation}</p>"
            : string.Empty;

        var htmlBody = $"""
            <h2>You've been invited to {evt.EventTitle}</h2>
            <p><strong>Date:</strong> {evt.EventDateTime:f}</p>
            {locationLine}
            <p>Please RSVP by <strong>{evt.TokenExpiresAt:d}</strong>:</p>
            <p style="margin-top:24px">
              <a href="{acceptUrl}"
                 style="padding:10px 20px;background:#22c55e;color:#fff;text-decoration:none;border-radius:4px;font-weight:bold;">
                 Accept
              </a>
              &nbsp;&nbsp;
              <a href="{declineUrl}"
                 style="padding:10px 20px;background:#ef4444;color:#fff;text-decoration:none;border-radius:4px;font-weight:bold;">
                 Decline
              </a>
            </p>
            """;

        var plainBody =
            $"You've been invited to {evt.EventTitle} on {evt.EventDateTime:f}.\n\n" +
            $"Accept:  {acceptUrl}\n" +
            $"Decline: {declineUrl}\n\n" +
            $"This link expires on {evt.TokenExpiresAt:d}.";

        await SendAsync(
            to: evt.ParticipantEmail,
            subject: $"You're invited: {evt.EventTitle}",
            htmlBody: htmlBody,
            plainBody: plainBody,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Invitation email sent to {Email} for event {EventId}.", evt.ParticipantEmail, evt.EventId);
    }

    public async Task SendCancellationAsync(EventCancelled evt, CancellationToken cancellationToken = default)
    {
        var htmlBody = $"""
            <h2>Event Cancelled: {evt.EventTitle}</h2>
            <p>We're sorry to let you know that the following event has been cancelled:</p>
            <p>
              <strong>{evt.EventTitle}</strong><br/>
              Originally scheduled for <strong>{evt.EventDateTime:f}</strong>
            </p>
            <p>Please contact the organizer directly for more information.</p>
            """;

        var plainBody =
            $"The event '{evt.EventTitle}' (scheduled for {evt.EventDateTime:f}) has been cancelled.\n" +
            "Please contact the organiser for more information.";

        foreach (var email in evt.AffectedParticipantEmails)
        {
            await SendAsync(
                to: email,
                subject: $"Cancelled: {evt.EventTitle}",
                htmlBody: htmlBody,
                plainBody: plainBody,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Cancellation email sent to {Email} for event {EventId}.", email, evt.EventId);
        }
    }

    private async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string plainBody,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            senderAddress: SenderAddress,
            content: new EmailContent(subject)
            {
                PlainText = plainBody,
                Html = htmlBody
            },
            recipients: new EmailRecipients([new EmailAddress(to)]));

        // WaitUntil.Started — fire-and-forget; Service Bus retry handles redelivery on failure.
        await emailClient.SendAsync(WaitUntil.Started, message, cancellationToken);
    }
}
