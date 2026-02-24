using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Application.Features.Events.Commands.UpdateEvent;

public record UpdateEventCommand(
    Guid EventId,
    string Title,
    string? Description,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity,
    string OrganizerId) : IRequest;

public sealed class UpdateEventCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateEventCommand>
{
    public async Task Handle(
        UpdateEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await context.Events
            .FirstOrDefaultAsync(e => e.Id == command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Event), command.EventId);

        if (ev.OrganizerId != command.OrganizerId)
            throw new ForbiddenException("Only the organizer can update this event.");

        ev.Update(
            command.Title,
            command.Description,
            command.DateTime,
            command.Location,
            command.Capacity);

        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.DateTime)
            .Must(dateTime => dateTime > DateTimeOffset.UtcNow)
            .WithMessage("Event date must be in the future.");

        RuleFor(x => x.Location)
            .MaximumLength(500);

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .When(x => x.Capacity.HasValue)
            .WithMessage("Capacity must be a positive number.");
    }
}
