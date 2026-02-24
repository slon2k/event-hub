using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Invitations.Commands.CancelInvitation;

public record CancelInvitationCommand(
    Guid EventId,
    Guid InvitationId,
    string OrganizerId) : IRequest;

public sealed class CancelInvitationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CancelInvitationCommand>
{
    public async Task Handle(
        CancelInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can cancel invitations for this event.");

        ev.CancelInvitation(command.InvitationId);
        await context.SaveChangesAsync(cancellationToken);
    }
}
