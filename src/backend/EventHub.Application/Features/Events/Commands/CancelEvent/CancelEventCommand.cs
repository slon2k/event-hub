using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Commands.CancelEvent;

public record CancelEventCommand(Guid EventId, string OrganizerId) : IRequest;

public sealed class CancelEventCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CancelEventCommand>
{
    public async Task Handle(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can cancel this event.");

        ev.Cancel();
        await context.SaveChangesAsync(cancellationToken);
    }
}
