using EventHub.Application.Abstractions;
using EventHub.Application.Features.Admin.GetAllEvents;
using EventHub.Domain.Entities;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Admin;

public class GetAllEventsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsAllEventsAcrossOrganizers()
    {
        var org1Event = CreateEvent("organizer-1");
        var org2Event = CreateEvent("organizer-2");
        var mockContext = BuildMockContext([org1Event, org2Event]);

        var result = await new GetAllEventsQueryHandler(mockContext.Object)
            .Handle(new GetAllEventsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_WhenNoEvents_ReturnsEmptyList()
    {
        var mockContext = BuildMockContext([]);

        var result = await new GetAllEventsQueryHandler(mockContext.Object)
            .Handle(new GetAllEventsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WhenCalled_ReturnsEventsSortedByDateTimeDescending()
    {
        var earlier = CreateEvent("organizer-1", DateTimeOffset.UtcNow.AddDays(3));
        var later   = CreateEvent("organizer-2", DateTimeOffset.UtcNow.AddDays(10));
        var mockContext = BuildMockContext([earlier, later]);

        var result = await new GetAllEventsQueryHandler(mockContext.Object)
            .Handle(new GetAllEventsQuery(), CancellationToken.None);

        Assert.Equal(later.Id,   result[0].Id);
        Assert.Equal(earlier.Id, result[1].Id);
    }

    [Fact]
    public async Task Handle_WhenCalled_MapsSummaryFields()
    {
        var ev = Event.Create(
            "Board Games Night",
            "Description",
            DateTimeOffset.UtcNow.AddDays(7),
            "Community Hall",
            20,
            "organizer-1");
        var mockContext = BuildMockContext([ev]);

        var result = await new GetAllEventsQueryHandler(mockContext.Object)
            .Handle(new GetAllEventsQuery(), CancellationToken.None);

        var dto = result[0];
        Assert.Equal(ev.Id,          dto.Id);
        Assert.Equal("Board Games Night", dto.Title);
        Assert.Equal("Community Hall",    dto.Location);
        Assert.Equal("Draft",             dto.Status);
        Assert.Equal("organizer-1",       dto.OrganizerId);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        return mockContext;
    }

    private static Event CreateEvent(
        string organizerId,
        DateTimeOffset? dateTime = null) =>
        Event.Create(
            "Test Event",
            null,
            dateTime ?? DateTimeOffset.UtcNow.AddDays(7),
            null,
            null,
            organizerId);
}
