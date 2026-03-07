using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Invitations.Commands.ReissueInvitationToken;
using EventHub.Domain.Entities;
using EventHub.Domain.Services;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class ReissueInvitationTokenCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CallSaveChanges_WhenTokenIsReissuedSuccessfully()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new ReissueInvitationTokenCommand(ev.Id, invitationId, "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_CallTokenServiceGenerate_WhenReissuing()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new ReissueInvitationTokenCommand(ev.Id, invitationId, "organizer-1"), CancellationToken.None);

        mockTokenService.Verify(
            s => s.Generate(invitationId, It.IsAny<string>(), It.IsAny<DateTimeOffset>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_ThrowNotFoundException_WhenEventDoesNotExist()
    {
        var (mockContext, mockTokenService) = BuildMocks([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new ReissueInvitationTokenCommand(Guid.NewGuid(), Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_ThrowForbiddenException_WhenOrganizerDoesNotMatch()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new ReissueInvitationTokenCommand(ev.Id, invitationId, "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_ThrowNotFoundException_WhenInvitationDoesNotExistInEvent()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out _);
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new ReissueInvitationTokenCommand(ev.Id, Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenEventNotFound()
    {
        var (mockContext, mockTokenService) = BuildMocks([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new ReissueInvitationTokenCommand(Guid.NewGuid(), Guid.NewGuid(), "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenOrganizerDoesNotMatch()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ReissueInvitationTokenCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new ReissueInvitationTokenCommand(ev.Id, invitationId, "different-organizer"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IApplicationDbContext> context, Mock<IRsvpTokenService> tokenService) BuildMocks(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var mockTokenService = new Mock<IRsvpTokenService>();
        mockTokenService
            .Setup(s => s.Generate(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
            .Returns(("new-raw-token", "new-token-hash"));

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
