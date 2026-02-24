namespace EventHub.Application.Features.Events.Queries;

public record EventSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity,
    string Status,
    int AcceptedCount,
    int PendingCount,
    DateTimeOffset CreatedAt);
