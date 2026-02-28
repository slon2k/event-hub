using EventHub.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Services;

/// <summary>
/// Development stub that writes email content to the application log instead of
/// sending via ACS. Enable by setting <c>AcsEmail:UseStub=true</c> in
/// <c>local.settings.json</c>.
/// </summary>
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendInvitationAsync(InvitationSent evt, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[STUB EMAIL] Invitation → {To} | Event: '{Title}' on {Date} | " +
            "InvitationId: {InvitationId} | Token: {Token} | Expires: {Expiry}",
            evt.ParticipantEmail,
            evt.EventTitle,
            evt.EventDateTime,
            evt.InvitationId,
            evt.RsvpToken,
            evt.TokenExpiresAt);

        return Task.CompletedTask;
    }

    public Task SendCancellationAsync(EventCancelled evt, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[STUB EMAIL] Cancellation → {Recipients} | Event: '{Title}' ({EventId}) on {Date}",
            string.Join(", ", evt.AffectedParticipantEmails),
            evt.EventTitle,
            evt.EventId,
            evt.EventDateTime);

        return Task.CompletedTask;
    }
}
