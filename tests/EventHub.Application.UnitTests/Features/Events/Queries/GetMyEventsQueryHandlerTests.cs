using EventHub.Application.Abstractions;
using EventHub.Application.Features.Events.Queries.GetMyEvents;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Queries;

public class GetMyEventsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsOnlyOrganizerEvents()
    {
        var ownerEvent = CreateEvent("organizer-1", DateTimeOffset.UtcNow.AddDays(7));
        var otherEvent = CreateEvent("organizer-2", DateTimeOffset.UtcNow.AddDays(7));
        var mockContext = BuildMockContext([ownerEvent, otherEvent]);

        var result = await new GetMyEventsQueryHandler(mockContext.Object)
            .Handle(new GetMyEventsQuery("organizer-1"), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(ownerEvent.Id, result[0].Id);
    }

    [Fact]
    public async Task Handle_WhenOrganizerHasNoEvents_ReturnsEmptyList()
    {
        var mockContext = BuildMockContext([]);

        var result = await new GetMyEventsQueryHandler(mockContext.Object)
            .Handle(new GetMyEventsQuery("organizer-1"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WhenCalled_ReturnsEventsSortedByDateTimeDescending()
    {
        var earlier = CreateEvent("organizer-1", DateTimeOffset.UtcNow.AddDays(3));
        var later = CreateEvent("organizer-1", DateTimeOffset.UtcNow.AddDays(10));
        var mockContext = BuildMockContext([earlier, later]);

        var result = await new GetMyEventsQueryHandler(mockContext.Object)
            .Handle(new GetMyEventsQuery("organizer-1"), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(later.Id, result[0].Id);
        Assert.Equal(earlier.Id, result[1].Id);
    }

    [Fact]
    public async Task Handle_WhenCalled_CountsAcceptedInvitationsCorrectly()
    {
        var ev = CreatePublishedEvent("organizer-1");
        ev.AddInvitation("accepted@example.com", "raw1", "hash1", DateTimeOffset.UtcNow.AddHours(72), Guid.NewGuid());
        var pendingInvitation = ev.AddInvitation("pending@example.com", "raw2", "hash2", DateTimeOffset.UtcNow.AddHours(72), Guid.NewGuid());
        ev.AcceptInvitation(ev.Invitations.First(i => i.ParticipantEmail == "accepted@example.com").Id);
        var mockContext = BuildMockContext([ev]);

        var result = await new GetMyEventsQueryHandler(mockContext.Object)
            .Handle(new GetMyEventsQuery("organizer-1"), CancellationToken.None);

        Assert.Equal(1, result[0].AcceptedCount);
        Assert.Equal(1, result[0].PendingCount);
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

        var result = await new GetMyEventsQueryHandler(mockContext.Object)
            .Handle(new GetMyEventsQuery("organizer-1"), CancellationToken.None);

        var dto = result[0];
        Assert.Equal(ev.Id, dto.Id);
        Assert.Equal("Board Games Night", dto.Title);
        Assert.Equal("Community Hall", dto.Location);
        Assert.Equal(20, dto.Capacity);
        Assert.Equal(EventStatus.Draft.ToString(), dto.Status);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        return mockContext;
    }

    private static Event CreateEvent(string organizerId, DateTimeOffset dateTime) =>
        Event.Create("Test Event", null, dateTime, null, null, organizerId);

    private static Event CreatePublishedEvent(string organizerId)
    {
        var ev = Event.Create("Test Event", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
        ev.Publish();
        return ev;
    }
}
