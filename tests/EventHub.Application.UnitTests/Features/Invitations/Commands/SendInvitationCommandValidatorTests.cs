using EventHub.Application.Features.Invitations.Commands.SendInvitation;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class SendInvitationCommandValidatorTests
{
    private readonly SendInvitationCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_HaveError_WhenParticipantEmailIsEmpty()
    {
        var command = ValidCommand() with { ParticipantEmail = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendInvitationCommand.ParticipantEmail));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenParticipantEmailIsInvalid()
    {
        var command = ValidCommand() with { ParticipantEmail = "not-an-email" };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendInvitationCommand.ParticipantEmail));
    }

    [Fact]
    public void Validate_Should_HaveError_WhenParticipantEmailExceedsMaxLength()
    {
        var localPart = new string('a', 251);
        var command = ValidCommand() with { ParticipantEmail = $"{localPart}@x.com" };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendInvitationCommand.ParticipantEmail));
    }

    [Fact]
    public void Validate_Should_Pass_ForValidCommand()
    {
        var command = ValidCommand();

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static SendInvitationCommand ValidCommand() => new(
        EventId: Guid.NewGuid(),
        ParticipantEmail: "guest@example.com",
        OrganizerId: "organizer-1");
}
