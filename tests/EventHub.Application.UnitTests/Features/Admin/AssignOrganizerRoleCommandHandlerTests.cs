using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Admin.AssignOrganizerRole;
using Moq;

namespace EventHub.Application.UnitTests.Features.Admin;

public class AssignOrganizerRoleCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTargetIsActingAdmin_ThrowsForbiddenException()
    {
        var service = new Mock<IIdentityAdminService>();
        var handler = new AssignOrganizerRoleCommandHandler(service.Object);
        var command = new AssignOrganizerRoleCommand("user-1", "user-1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTargetIsActingAdmin_DoesNotCallService()
    {
        var service = new Mock<IIdentityAdminService>();
        var handler = new AssignOrganizerRoleCommandHandler(service.Object);
        var command = new AssignOrganizerRoleCommand("admin-1", "admin-1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));

        service.Verify(
            s => s.AssignOrganizerRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetIsDifferentUser_CallsServiceWithCorrectIds()
    {
        var service = new Mock<IIdentityAdminService>();
        service
            .Setup(s => s.AssignOrganizerRoleAsync("user-2", "admin-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AssignOrganizerRoleCommandHandler(service.Object);
        var command = new AssignOrganizerRoleCommand("user-2", "admin-1");

        await handler.Handle(command, CancellationToken.None);

        service.Verify(
            s => s.AssignOrganizerRoleAsync("user-2", "admin-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
