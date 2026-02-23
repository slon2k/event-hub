using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Invitations.Commands.ReissueInvitationToken;

public record ReissueInvitationTokenCommand(
    Guid EventId,
    Guid InvitationId,
    string OrganizerId) : IRequest;

public sealed class ReissueInvitationTokenCommandHandler(
    IApplicationDbContext context,
    IRsvpTokenService tokenService)
    : IRequestHandler<ReissueInvitationTokenCommand>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    public async Task Handle(
        ReissueInvitationTokenCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can reissue invitation tokens for this event.");

        var invitation = ev.Invitations.FirstOrDefault(i => i.Id == command.InvitationId)
            ?? throw new NotFoundException("Invitation", command.InvitationId);

        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var (rawToken, tokenHash) = tokenService.Generate(
            command.InvitationId,
            invitation.ParticipantEmail,
            expiresAt);

        ev.ReissueInvitationToken(command.InvitationId, rawToken, tokenHash, expiresAt);
        await context.SaveChangesAsync(cancellationToken);
    }
}
