using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Queries.GetEventById;

public record GetEventByIdQuery(Guid EventId) : IRequest<EventDetailDto>;

public sealed class GetEventByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetEventByIdQuery, EventDetailDto>
{
    public async Task<EventDetailDto> Handle(
        GetEventByIdQuery query,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .AsNoTracking()
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == query.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), query.EventId);

        return new EventDetailDto(
            ev.Id,
            ev.Title,
            ev.Description,
            ev.DateTime,
            ev.Location,
            ev.Capacity,
            ev.Status.ToString(),
            ev.OrganizerId,
            ev.CreatedAt,
            ev.UpdatedAt,
            ev.Invitations.Select(i => new InvitationDto(
                i.Id,
                i.ParticipantEmail,
                i.Status.ToString(),
                i.SentAt,
                i.RespondedAt)).ToList());
    }
}
