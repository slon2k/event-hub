namespace EventHub.Application.Features.Admin;

public sealed record AdminUserDto(
    string UserId,
    string? DisplayName,
    string? Email,
    bool IsOrganizer,
    bool IsAdmin);
