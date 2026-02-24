using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Invitations.Commands.RespondToInvitation;

public enum InvitationResponse { Accept, Decline }

public record RespondToInvitationCommand(
    Guid InvitationId,
    string RawToken,
    InvitationResponse Response) : IRequest;

public sealed class RespondToInvitationCommandHandler(
    IApplicationDbContext context,
    IRsvpTokenService tokenService)
    : IRequestHandler<RespondToInvitationCommand>
{
    public async Task Handle(
        RespondToInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(
                e => e.Invitations.Any(i => i.Id == command.InvitationId),
                cancellationToken)
            ?? throw new NotFoundException("Invitation", command.InvitationId);

        var invitation = ev.Invitations.Single(i => i.Id == command.InvitationId);

        if (invitation.RsvpTokenHash is null || invitation.RsvpTokenExpiresAt is null)
            throw new Domain.Common.DomainException("This invitation token has already been used or cancelled.");

        if (!tokenService.IsValid(command.RawToken, invitation.RsvpTokenHash, invitation.RsvpTokenExpiresAt.Value))
            throw new Domain.Common.DomainException("The invitation token is invalid or has expired.");

        if (command.Response == InvitationResponse.Accept)
            ev.AcceptInvitation(command.InvitationId);
        else
            ev.DeclineInvitation(command.InvitationId);

        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RespondToInvitationCommandValidator : AbstractValidator<RespondToInvitationCommand>
{
    public RespondToInvitationCommandValidator()
    {
        RuleFor(x => x.RawToken).NotEmpty();
    }
}
