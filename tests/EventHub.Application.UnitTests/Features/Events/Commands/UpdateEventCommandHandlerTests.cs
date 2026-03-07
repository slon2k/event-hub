using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Events.Commands.UpdateEvent;
using EventHub.Domain.Entities;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class UpdateEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrganizerMatches_UpdatesEventFields()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);
        var newDate = DateTimeOffset.UtcNow.AddDays(14);

        await new UpdateEventCommandHandler(mockContext.Object).Handle(
            new UpdateEventCommand(ev.Id, "Updated Title", "New desc", newDate, "New Location", 50, "organizer-1"),
            CancellationToken.None);

        Assert.Equal("Updated Title", ev.Title);
        Assert.Equal("New desc", ev.Description);
        Assert.Equal(newDate, ev.DateTime);
        Assert.Equal("New Location", ev.Location);
        Assert.Equal(50, ev.Capacity);
    }

    [Fact]
    public async Task Handle_WhenUpdateSucceeds_CallsSaveChanges()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await new UpdateEventCommandHandler(mockContext.Object).Handle(
            ValidCommand(ev.Id, "organizer-1"), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateEventCommandHandler(mockContext.Object)
                .Handle(ValidCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_ThrowsForbiddenException()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new UpdateEventCommandHandler(mockContext.Object)
                .Handle(ValidCommand(ev.Id, "different-organizer"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEventNotFound_DoesNotCallSaveChanges()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateEventCommandHandler(mockContext.Object)
                .Handle(ValidCommand(Guid.NewGuid(), "organizer-1"), CancellationToken.None));

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrganizerDoesNotMatch_DoesNotCallSaveChanges()
    {
        var ev = CreateEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new UpdateEventCommandHandler(mockContext.Object)
                .Handle(ValidCommand(ev.Id, "different-organizer"), CancellationToken.None));

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
        Event.Create("Original Title", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);

    private static UpdateEventCommand ValidCommand(Guid eventId, string organizerId) => new(
        EventId: eventId,
        Title: "Updated Title",
        Description: null,
        DateTime: DateTimeOffset.UtcNow.AddDays(14),
        Location: null,
        Capacity: null,
        OrganizerId: organizerId);
}
