using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Events.Queries.GetEventById;
using EventHub.Domain.Entities;
using EventHub.Domain.Enumerations;
using MockQueryable.Moq;
using Moq;

namespace EventHub.Application.UnitTests.Features.Events.Queries;

public class GetEventByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenEventExists_ReturnsEventDetailDto()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        var result = await new GetEventByIdQueryHandler(mockContext.Object)
            .Handle(new GetEventByIdQuery(ev.Id), CancellationToken.None);

        Assert.Equal(ev.Id, result.Id);
        Assert.Equal(ev.Title, result.Title);
        Assert.Equal(ev.OrganizerId, result.OrganizerId);
        Assert.Equal(ev.Status.ToString(), result.Status);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        var mockContext = BuildMockContext([]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetEventByIdQueryHandler(mockContext.Object)
                .Handle(new GetEventByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenEventHasInvitations_MapsInvitations()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var invitation = ev.AddInvitation(
            "guest@example.com", "raw-token", "token-hash",
            DateTimeOffset.UtcNow.AddHours(72));
        var mockContext = BuildMockContext([ev]);

        var result = await new GetEventByIdQueryHandler(mockContext.Object)
            .Handle(new GetEventByIdQuery(ev.Id), CancellationToken.None);

        Assert.Single(result.Invitations);
        var invitationDto = result.Invitations[0];
        Assert.Equal(invitation.Id, invitationDto.Id);
        Assert.Equal("guest@example.com", invitationDto.ParticipantEmail);
        Assert.Equal(InvitationStatus.Pending.ToString(), invitationDto.Status);
    }

    [Fact]
    public async Task Handle_WhenEventHasNoInvitations_ReturnsEmptyInvitationList()
    {
        var ev = CreatePublishedEvent("organizer-1");
        var mockContext = BuildMockContext([ev]);

        var result = await new GetEventByIdQueryHandler(mockContext.Object)
            .Handle(new GetEventByIdQuery(ev.Id), CancellationToken.None);

        Assert.Empty(result.Invitations);
    }

    [Fact]
    public async Task Handle_WhenCalled_MapsAllDetailFields()
    {
        var ev = Event.Create(
            "Board Games Night",
            "Bring your favourite game.",
            DateTimeOffset.UtcNow.AddDays(7),
            "Community Hall",
            20,
            "organizer-1");
        ev.Publish();
        var mockContext = BuildMockContext([ev]);

        var result = await new GetEventByIdQueryHandler(mockContext.Object)
            .Handle(new GetEventByIdQuery(ev.Id), CancellationToken.None);

        Assert.Equal("Board Games Night", result.Title);
        Assert.Equal("Bring your favourite game.", result.Description);
        Assert.Equal("Community Hall", result.Location);
        Assert.Equal(20, result.Capacity);
    }

    private static Mock<IApplicationDbContext> BuildMockContext(List<Event> events)
    {
        var mockContext = new Mock<IApplicationDbContext>();
        mockContext.Setup(c => c.Events).Returns(events.BuildMockDbSet().Object);
        return mockContext;
    }

    private static Event CreatePublishedEvent(string organizerId)
    {
        var ev = Event.Create("Test Event", null, DateTimeOffset.UtcNow.AddDays(7), null, null, organizerId);
        ev.Publish();
        return ev;
    }
}
