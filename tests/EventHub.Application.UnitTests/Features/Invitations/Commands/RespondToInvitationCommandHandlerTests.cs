using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Invitations.Commands.RespondToInvitation;
using EventHub.Domain.Common;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using EventHub.Domain.Services;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class RespondToInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTokenIsValidAndResponseIsAccept_AcceptsInvitation()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: true);

        await new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new RespondToInvitationCommand(invitationId, "raw-token", InvitationResponse.Accept), CancellationToken.None);

        var invitation = ev.Invitations.Single(i => i.Id == invitationId);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public async Task Handle_WhenTokenIsValidAndResponseIsDecline_DeclinesInvitation()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: true);

        await new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new RespondToInvitationCommand(invitationId, "raw-token", InvitationResponse.Decline), CancellationToken.None);

        var invitation = ev.Invitations.Single(i => i.Id == invitationId);
        Assert.Equal(InvitationStatus.Declined, invitation.Status);
    }

    [Fact]
    public async Task Handle_WhenResponseSucceeds_CallsSaveChanges()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: true);

        await new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new RespondToInvitationCommand(invitationId, "raw-token", InvitationResponse.Accept), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvitationDoesNotExist_ThrowsNotFoundException()
    {
        var (mockContext, mockTokenService) = BuildMocks([], tokenIsValid: true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new RespondToInvitationCommand(Guid.NewGuid(), "raw-token", InvitationResponse.Accept), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTokenAlreadyUsed_ThrowsDomainException()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);

        // Accept the invitation first so token is cleared (single-use)
        ev.AcceptInvitation(invitationId);

        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: true);

        await Assert.ThrowsAsync<DomainException>(() =>
            new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new RespondToInvitationCommand(invitationId, "raw-token", InvitationResponse.Accept), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTokenIsInvalidOrExpired_ThrowsDomainException()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: false);

        await Assert.ThrowsAsync<DomainException>(() =>
            new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new RespondToInvitationCommand(invitationId, "wrong-token", InvitationResponse.Accept), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTokenIsInvalid_DoesNotCallSaveChanges()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev], tokenIsValid: false);

        await Assert.ThrowsAsync<DomainException>(() =>
            new RespondToInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new RespondToInvitationCommand(invitationId, "wrong-token", InvitationResponse.Accept), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IApplicationDbContext> context, Mock<IRsvpTokenService> tokenService) BuildMocks(
        List<Event> events, bool tokenIsValid)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var mockTokenService = new Mock<IRsvpTokenService>();
        mockTokenService
            .Setup(s => s.IsValid(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(tokenIsValid);

        return (mockContext, mockTokenService);
    }

    private static Event CreatePublishedEventWithInvitation(string organizerId, out Guid invitationId)
    {
        var ev = Event.Create("Test Event", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
        ev.Publish();
        var invitation = ev.AddInvitation(
            "guest@example.com", "raw-token", "token-hash",
            DateTimeOffset.UtcNow.AddHours(72));
        invitationId = invitation.Id;
        return ev;
    }
}
