using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Invitations.Commands.CancelInvitation;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class CancelInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrganizerMatches_CancelsInvitation()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var mockContext = BuildMockContext([ev]);

        await new CancelInvitationCommandHandler(mockContext.Object)
            .Handle(new CancelInvitationCommand(ev.Id, invitationId, "organizer-1"), CancellationToken.None);

        var invitation = ev.Invitations.Single(i => i.Id == invitationId);
        Assert.Equal(InvitationStatus.Cancelled, invitation.Status);
    }

    [Fact]
    public async Task Handle_WhenCancelSucceeds_CallsSaveChanges()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var mockContext = BuildMockContext([ev]);

        await new CancelInvitationCommandHandler(mockContext.Object)
            .Handle(new CancelInvitationCommand(ev.Id, invitationId, "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelInvitationCommandHandler(mockContext.Object)
                .Handle(new CancelInvitationCommand(Guid.NewGuid(), Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_ThrowsForbiddenException()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CancelInvitationCommandHandler(mockContext.Object)
                .Handle(new CancelInvitationCommand(ev.Id, invitationId, "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEventNotFound_DoesNotCallSaveChanges()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelInvitationCommandHandler(mockContext.Object)
                .Handle(new CancelInvitationCommand(Guid.NewGuid(), Guid.NewGuid(), "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_DoesNotCallSaveChanges()
    {
        var ev = CreatePublishedEventWithInvitation("organizer-1", out var invitationId);
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CancelInvitationCommandHandler(mockContext.Object)
                .Handle(new CancelInvitationCommand(ev.Id, invitationId, "different-organizer"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return mockContext;
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
