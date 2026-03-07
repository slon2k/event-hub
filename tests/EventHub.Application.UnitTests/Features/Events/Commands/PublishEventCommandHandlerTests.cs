using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Events.Commands.PublishEvent;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class PublishEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_SetStatusToPublished_WhenOrganizerMatches()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await new PublishEventCommandHandler(mockContext.Object)
            .Handle(new PublishEventCommand(ev.Id, "organizer-1"), CancellationToken.None);

        Assert.Equal(EventStatus.Published, ev.Status);
    }

    [Fact]
    public async Task Handle_Should_CallSaveChanges_WhenPublishSucceeds()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await new PublishEventCommandHandler(mockContext.Object)
            .Handle(new PublishEventCommand(ev.Id, "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_ThrowNotFoundException_WhenEventDoesNotExist()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new PublishEventCommandHandler(mockContext.Object)
                .Handle(new PublishEventCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_ThrowForbiddenException_WhenOrganizerDoesNotMatch()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new PublishEventCommandHandler(mockContext.Object)
                .Handle(new PublishEventCommand(ev.Id, "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenEventNotFound()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new PublishEventCommandHandler(mockContext.Object)
                .Handle(new PublishEventCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenOrganizerDoesNotMatch()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new PublishEventCommandHandler(mockContext.Object)
                .Handle(new PublishEventCommand(ev.Id, "different-organizer"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return mockContext;
    }

    private static Event CreateEvent(string organizerId) =>
        Event.Create("Test Event", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
}
