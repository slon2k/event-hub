namespace EventHub.Application.Features.Events.Queries;

public record InvitationDto(
    Guid Id,
    string ParticipantEmail,
    string Status,
    DateTimeOffset SentAt,
    DateTimeOffset? RespondedAt);

public record EventDetailDto(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity,
    string Status,
    string OrganizerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<InvitationDto> Invitations);
