using EventHub.Domain.Events;

namespace EventHub.Notifications.Services;

public interface IEmailSender
{
    Task SendInvitationAsync(InvitationSent message, CancellationToken cancellationToken = default);

    Task SendCancellationAsync(EventCancelled message, CancellationToken cancellationToken = default);
}
