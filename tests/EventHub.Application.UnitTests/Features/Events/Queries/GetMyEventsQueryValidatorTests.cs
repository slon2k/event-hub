using EventHub.Application.Features.Events.Queries.GetMyEvents;

namespace EventHub.Application.UnitTests.Features.Events.Queries;

public class GetMyEventsQueryValidatorTests
{
    private readonly GetMyEventsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenStatusIsNull_Passes()
    {
        var result = _validator.Validate(new GetMyEventsQuery("org-1", null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Published")]
    [InlineData("Cancelled")]
    public void Validate_WhenStatusIsValid_Passes(string status)
    {
        var result = _validator.Validate(new GetMyEventsQuery("org-1", status));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("PUBLISHED")]
    [InlineData("cancelled")]
    public void Validate_WhenStatusIsCaseInsensitive_Passes(string status)
    {
        var result = _validator.Validate(new GetMyEventsQuery("org-1", status));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("Active")]
    [InlineData("")]
    public void Validate_WhenStatusIsUnrecognised_ReturnsValidationError(string status)
    {
        var result = _validator.Validate(new GetMyEventsQuery("org-1", status));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(nameof(GetMyEventsQuery.Status), result.Errors[0].PropertyName);
    }
}
