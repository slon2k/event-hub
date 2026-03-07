using EventHub.Application.Features.Events.Commands.CreateEvent;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class CreateEventCommandValidatorTests
{
    private readonly CreateEventCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_HaveError_WhenTitleIsEmpty()
    {
        var command = ValidCommand() with { Title = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenTitleExceedsMaxLength()
    {
        var command = ValidCommand() with { Title = new string('a', 201) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        var command = ValidCommand() with { Description = new string('a', 2001) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Description));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenDateIsNotInFuture()
    {
        var command = ValidCommand() with { DateTime = DateTimeOffset.UtcNow.AddMinutes(-1) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.DateTime));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenLocationExceedsMaxLength()
    {
        var command = ValidCommand() with { Location = new string('a', 501) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Location));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Should_HaveError_WhenCapacityIsNotPositive(int capacity)
    {
        var command = ValidCommand() with { Capacity = capacity };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Capacity));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenOrganizerIdIsEmpty()
    {
        var command = ValidCommand() with { OrganizerId = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.OrganizerId));
    }

    [Fact]
    public void Validate_Should_Pass_ForValidCommand()
    {
        var command = ValidCommand();

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static CreateEventCommand ValidCommand() => new(
        Title: "Board Games Night",
        Description: "Bring your favorite game.",
        DateTime: DateTimeOffset.UtcNow.AddDays(7),
        Location: "Community Hall",
        Capacity: 20,
        OrganizerId: "organizer-1");
}
