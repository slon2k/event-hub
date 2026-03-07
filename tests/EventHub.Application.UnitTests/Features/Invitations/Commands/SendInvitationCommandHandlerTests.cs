using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Invitations.Commands.SendInvitation;
using EventHub.Domain.Entities;
using EventHub.Domain.Services;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Invitations.Commands;

public class SendInvitationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrganizerMatchesAndEventIsPublished_ReturnsInvitationId()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        var result = await new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new SendInvitationCommand(ev.Id, "guest@example.com", "organizer-1"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public async Task Handle_WhenInvitationIsCreated_CallsSaveChanges()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
            .Handle(new SendInvitationCommand(ev.Id, "guest@example.com", "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        var (mockContext, mockTokenService) = BuildMocks([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new SendInvitationCommand(Guid.NewGuid(), "guest@example.com", "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_ThrowsForbiddenException()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new SendInvitationCommand(ev.Id, "guest@example.com", "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEventNotFound_DoesNotCallSaveChanges()
    {
        var (mockContext, mockTokenService) = BuildMocks([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new SendInvitationCommand(Guid.NewGuid(), "guest@example.com", "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_DoesNotCallSaveChanges()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var (mockContext, mockTokenService) = BuildMocks([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new SendInvitationCommandHandler(mockContext.Object, mockTokenService.Object)
                .Handle(new SendInvitationCommand(ev.Id, "guest@example.com", "different-organizer"), CancellationToken.None));

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
            .Returns(("raw-token", "token-hash"));

        return (mockContext, mockTokenService);
    }

    private static Event CreatePublishedEvent(string organizerId)
    {
        var ev = Event.Create("Test Event", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
        ev.Publish();
        return ev;
    }
}
