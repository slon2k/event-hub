using EventHub.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Admin.GetAllEvents;

public record GetAllEventsQuery : IRequest<IReadOnlyList<AdminEventSummaryDto>>;

public sealed class GetAllEventsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAllEventsQuery, IReadOnlyList<AdminEventSummaryDto>>
{
    public async Task<IReadOnlyList<AdminEventSummaryDto>> Handle(
        GetAllEventsQuery query,
        CancellationToken cancellationToken)
    {
        return await context.Events
            .AsNoTracking()
            .OrderByDescending(e => e.DateTime)
            .Select(e => new AdminEventSummaryDto(
                e.Id,
                e.Title,
                e.DateTime,
                e.Location,
                e.Status.ToString(),
                e.OrganizerId,
                e.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
