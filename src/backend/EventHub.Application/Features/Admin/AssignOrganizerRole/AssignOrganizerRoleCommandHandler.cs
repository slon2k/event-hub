using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using MediatR;

namespace EventHub.Application.Features.Admin.AssignOrganizerRole;

public record AssignOrganizerRoleCommand(string TargetUserId, string ActingAdminUserId) : IRequest;

public sealed class AssignOrganizerRoleCommandHandler(IIdentityAdminService identityAdminService)
    : IRequestHandler<AssignOrganizerRoleCommand>
{
    public Task Handle(
        AssignOrganizerRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TargetUserId == command.ActingAdminUserId)
            throw new ForbiddenException("An admin cannot modify their own roles.");

        return identityAdminService.AssignOrganizerRoleAsync(
            command.TargetUserId,
            command.ActingAdminUserId,
            cancellationToken);
    }
}
