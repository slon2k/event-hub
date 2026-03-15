using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;

namespace EventHub.Application.Features.Admin.RemoveOrganizerRole;

public record RemoveOrganizerRoleCommand(string TargetUserId, string ActingAdminUserId) : IRequest;

public sealed class RemoveOrganizerRoleCommandHandler(IIdentityAdminService identityAdminService)
    : IRequestHandler<RemoveOrganizerRoleCommand>
{
    public Task Handle(
        RemoveOrganizerRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TargetUserId == command.ActingAdminUserId)
            throw new ForbiddenException("An admin cannot modify their own roles.");

        return identityAdminService.RemoveOrganizerRoleAsync(
            command.TargetUserId,
            command.ActingAdminUserId,
            cancellationToken);
    }
}
