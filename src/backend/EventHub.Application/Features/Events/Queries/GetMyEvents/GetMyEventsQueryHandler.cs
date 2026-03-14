using EventHub.Application.Abstractions;
using EventHub.Domain.Enumerations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Queries.GetMyEvents;

public record GetMyEventsQuery(string OrganizerId, string? Status = null) : IRequest<IReadOnlyList<EventSummaryDto>>;

public sealed class GetMyEventsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMyEventsQuery, IReadOnlyList<EventSummaryDto>>
{
    public async Task<IReadOnlyList<EventSummaryDto>> Handle(
        GetMyEventsQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == query.OrganizerId);

        if (query.Status is not null
            && Enum.TryParse<EventStatus>(query.Status, ignoreCase: true, out var statusFilter))
        {
            dbQuery = dbQuery.Where(e => e.Status == statusFilter);
        }

        return await dbQuery
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
                e.Invitations.Count(i => i.Status == InvitationStatus.Declined),
                e.Invitations.Count(),
                e.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetMyEventsQueryValidator : AbstractValidator<GetMyEventsQuery>
{
    private static readonly HashSet<string> ValidStatuses =
        Enum.GetNames<EventStatus>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    public GetMyEventsQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || ValidStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<EventStatus>())}.");
    }
}
