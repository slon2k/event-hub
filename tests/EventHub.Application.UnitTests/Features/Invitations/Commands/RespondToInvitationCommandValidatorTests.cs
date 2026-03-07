using EventHub.Application.Features.Invitations.Commands.RespondToInvitation;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class RespondToInvitationCommandValidatorTests
{
    private readonly RespondToInvitationCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_HaveError_WhenRawTokenIsEmpty()
    {
        var command = ValidCommand() with { RawToken = string.Empty };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RespondToInvitationCommand.RawToken));
    }

    [Fact]
    public void Validate_Should_Pass_ForValidCommand()
    {
        var command = ValidCommand();

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static RespondToInvitationCommand ValidCommand() => new(
        InvitationId: Guid.NewGuid(),
        RawToken: "valid-token",
        Response: InvitationResponse.Accept);
}
