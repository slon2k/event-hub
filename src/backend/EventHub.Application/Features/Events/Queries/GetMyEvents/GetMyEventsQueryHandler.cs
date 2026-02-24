using EventHub.Application.Abstractions;
using EventHub.Domain.Enumerations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Queries.GetMyEvents;

public record GetMyEventsQuery(string OrganizerId) : IRequest<IReadOnlyList<EventSummaryDto>>;

public sealed class GetMyEventsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMyEventsQuery, IReadOnlyList<EventSummaryDto>>
{
    public async Task<IReadOnlyList<EventSummaryDto>> Handle(
        GetMyEventsQuery query,
        CancellationToken cancellationToken)
    {
        return await context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == query.OrganizerId)
            .OrderByDescending(e => e.DateTime)
            .Select(e => new EventSummaryDto(
                e.Id,
                e.Title,
                e.DateTime,
                e.Location,
                e.Capacity,
                e.Status.ToString(),
                e.Invitations.Count(i => i.Status == InvitationStatus.Accepted),
                e.Invitations.Count(i => i.Status == InvitationStatus.Pending),
                e.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
