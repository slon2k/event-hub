namespace EventHub.Application.Features.Admin;

public sealed record AdminEventSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset DateTime,
    string? Location,
    string Status,
    string OrganizerId,
    DateTimeOffset CreatedAt);
