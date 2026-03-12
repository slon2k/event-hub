using EventHub.Application.Abstractions;
using EventHub.Application.Exceptions;
using EventHub.Application.Features.Admin.RemoveOrganizerRole;
using Moq;

namespace EventHub.Application.UnitTests.Features.Admin;

public class RemoveOrganizerRoleCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTargetIsActingAdmin_ThrowsForbiddenException()
    {
        var service = new Mock<IIdentityAdminService>();
        var handler = new RemoveOrganizerRoleCommandHandler(service.Object);
        var command = new RemoveOrganizerRoleCommand("user-1", "user-1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenTargetIsActingAdmin_DoesNotCallService()
    {
        var service = new Mock<IIdentityAdminService>();
        var handler = new RemoveOrganizerRoleCommandHandler(service.Object);
        var command = new RemoveOrganizerRoleCommand("admin-1", "admin-1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(command, CancellationToken.None));

        service.Verify(
            s => s.RemoveOrganizerRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetIsDifferentUser_CallsServiceWithCorrectIds()
    {
        var service = new Mock<IIdentityAdminService>();
        service
            .Setup(s => s.RemoveOrganizerRoleAsync("user-2", "admin-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RemoveOrganizerRoleCommandHandler(service.Object);
        var command = new RemoveOrganizerRoleCommand("user-2", "admin-1");

        await handler.Handle(command, CancellationToken.None);

        service.Verify(
            s => s.RemoveOrganizerRoleAsync("user-2", "admin-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
