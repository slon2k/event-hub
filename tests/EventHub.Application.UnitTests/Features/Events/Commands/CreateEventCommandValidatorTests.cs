using EventHub.Application.Features.Events.Commands.CreateEvent;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class CreateEventCommandValidatorTests
{
    private readonly CreateEventCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenTitleIsEmpty_ReturnsValidationError()
    {
        var command = ValidCommand() with { Title = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Validate_WhenTitleExceedsMaxLength_ReturnsValidationError()
    {
        var command = ValidCommand() with { Title = new string('a', 201) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Validate_WhenDescriptionExceedsMaxLength_ReturnsValidationError()
    {
        var command = ValidCommand() with { Description = new string('a', 2001) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Description));
    }

    [Fact]
    public void Validate_WhenDateIsNotInFuture_ReturnsValidationError()
    {
        var command = ValidCommand() with { DateTime = DateTimeOffset.UtcNow.AddMinutes(-1) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.DateTime));
    }

    [Fact]
    public void Validate_WhenLocationExceedsMaxLength_ReturnsValidationError()
    {
        var command = ValidCommand() with { Location = new string('a', 501) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Location));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenCapacityIsNotPositive_ReturnsValidationError(int capacity)
    {
        var command = ValidCommand() with { Capacity = capacity };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.Capacity));
    }

    [Fact]
    public void Validate_WhenOrganizerIdIsEmpty_ReturnsValidationError()
    {
        var command = ValidCommand() with { OrganizerId = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEventCommand.OrganizerId));
    }

    [Fact]
    public void Validate_WhenCalled_Passes_ForValidCommand()
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
