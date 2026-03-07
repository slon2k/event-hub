using EventHub.Application.Abstractions;
using EventHub.Application.Features.Events.Commands.CreateEvent;
using EventHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Commands;

public class CreateEventCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsNewGuid()
    {
        var mockDbSet = new Mock<DbSet<Event>>();
        var mockContext = BuildMockContext(mockDbSet);

        var handler = new CreateEventCommandHandler(mockContext.Object);
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public async Task Handle_WhenCalled_AddsEventToContext()
    {
        Event? captured = null;
        var mockDbSet = new Mock<DbSet<Event>>();
        mockDbSet.Setup(d => d.Add(It.IsAny<Event>())).Callback<Event>(e => captured = e);
        var mockContext = BuildMockContext(mockDbSet);

        var command = new CreateEventCommand(
            Title: "Board Games Night",
            Description: "Bring your favourite game.",
            DateTime: DateTimeOffset.UtcNow.AddDays(7),
            Location: "Community Hall",
            Capacity: 20,
            OrganizerId: "organizer-1");

        await new CreateEventCommandHandler(mockContext.Object).Handle(command, CancellationToken.None);

        mockDbSet.Verify(d => d.Add(It.IsAny<Event>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal("Board Games Night", captured.Title);
        Assert.Equal("organizer-1", captured.OrganizerId);
        Assert.Equal(20, captured.Capacity);
    }

    [Fact]
    public async Task Handle_WhenCalled_CallsSaveChangesOnce()
    {
        var mockDbSet = new Mock<DbSet<Event>>();
        var mockContext = BuildMockContext(mockDbSet);

        await new CreateEventCommandHandler(mockContext.Object).Handle(ValidCommand(), CancellationToken.None);

        mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCalled_ReturnsIdMatchingAddedEvent()
    {
        Event? captured = null;
        var mockDbSet = new Mock<DbSet<Event>>();
        mockDbSet.Setup(d => d.Add(It.IsAny<Event>())).Callback<Event>(e => captured = e);
        var mockContext = BuildMockContext(mockDbSet);

        var result = await new CreateEventCommandHandler(mockContext.Object).Handle(ValidCommand(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(captured.Id, result);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(Mock<DbSet<Event>> mockDbSet)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(mockDbSet.Object);
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return mockContext;
    }

    private static CreateEventCommand ValidCommand() => new(
        Title: "Test Event",
        Description: null,
        DateTime: DateTimeOffset.UtcNow.AddDays(7),
        Location: null,
        Capacity: null,
        OrganizerId: "organizer-1");
}
