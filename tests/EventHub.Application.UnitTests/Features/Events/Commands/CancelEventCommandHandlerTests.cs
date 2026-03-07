using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Events.Commands.CancelEvent;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class CancelEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CancelEvent_WhenOrganizerMatches()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);
        var handler = new CancelEventCommandHandler(mockContext.Object);

        await handler.Handle(new CancelEventCommand(ev.Id, "organizer-1"), CancellationToken.None);

        Assert.Equal(EventStatus.Cancelled, ev.Status);
    }

    [Fact]
    public async Task Handle_Should_CallSaveChanges_WhenCancelSucceeds()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await new CancelEventCommandHandler(mockContext.Object)
            .Handle(new CancelEventCommand(ev.Id, "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_ThrowNotFoundException_WhenEventDoesNotExist()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelEventCommandHandler(mockContext.Object)
                .Handle(new CancelEventCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_ThrowForbiddenException_WhenOrganizerDoesNotMatch()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CancelEventCommandHandler(mockContext.Object)
                .Handle(new CancelEventCommand(ev.Id, "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenEventNotFound()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelEventCommandHandler(mockContext.Object)
                .Handle(new CancelEventCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_NotCallSaveChanges_WhenOrganizerDoesNotMatch()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new CancelEventCommandHandler(mockContext.Object)
                .Handle(new CancelEventCommand(ev.Id, "different-organizer"), CancellationToken.None));

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
