using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Invitations.Commands.SendInvitation;

public record SendInvitationCommand(
    Guid EventId,
    string ParticipantEmail,
    string OrganizerId) : IRequest<Guid>;

public sealed class SendInvitationCommandHandler(
    IApplicationDbContext context,
    IRsvpTokenService tokenService)
    : IRequestHandler<SendInvitationCommand, Guid>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    public async Task<Guid> Handle(
        SendInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .Include(e => e.Invitations)
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can send invitations for this event.");

        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var invitationId = Guid.NewGuid();
        var (rawToken, tokenHash) = tokenService.Generate(
            invitationId,
            command.ParticipantEmail,
            expiresAt);

        var invitation = ev.AddInvitation(
            command.ParticipantEmail,
            rawToken,
            tokenHash,
            expiresAt,
            invitationId);

        await context.SaveChangesAsync(cancellationToken);

        return invitation.Id;
    }
}

public sealed class SendInvitationCommandValidator : AbstractValidator<SendInvitationCommand>
{
    public SendInvitationCommandValidator()
    {
        RuleFor(x => x.ParticipantEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}
