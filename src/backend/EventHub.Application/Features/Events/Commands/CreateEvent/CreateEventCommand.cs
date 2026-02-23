using EventHub.Application.Abstractions;
using EventHub.Domain.Entities;
using FluentValidation;
using MediatR;

namespace EventHub.Application.Features.Events.Commands.CreateEvent;

public record CreateEventCommand(
    string Title,
    string? Description,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity,
    string OrganizerId) : IRequest<Guid>;

public sealed class CreateEventCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateEventCommand, Guid>
{
    public async Task<Guid> Handle(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = Event.Create(
            command.Title,
            command.Description,
            command.DateTime,
            command.Location,
            command.Capacity,
            command.OrganizerId);

        context.Events.Add(ev);
        await context.SaveChangesAsync(cancellationToken);

        return ev.Id;
    }
}

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.DateTime)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Event date must be in the future.");

        RuleFor(x => x.Location)
            .MaximumLength(500);

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .When(x => x.Capacity.HasValue)
            .WithMessage("Capacity must be a positive number.");

        RuleFor(x => x.OrganizerId)
            .NotEmpty();
    }
}
