using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Commands.PublishEvent;

public record PublishEventCommand(Guid EventId, string OrganizerId) : IRequest;

public sealed class PublishEventCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PublishEventCommand>
{
    public async Task Handle(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can publish this event.");

        ev.Publish();
        await context.SaveChangesAsync(cancellationToken);
    }
}
