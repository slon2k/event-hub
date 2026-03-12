using EventHub.Application.Abstractions;
using EventHub.Application.Common;
using EventHub.Application.Features.Admin;
using EventHub.Application.Features.Admin.GetUsers;
using Moq;

namespace EventHub.Application.UnitTests.Features.Admin;

public class GetUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToServiceWithQueryParameters()
    {
        var expected = new PagedResult<AdminUserDto>(
            [new AdminUserDto("u1", "Alice", "alice@example.com", true, false)],
            Page: 2, PageSize: 10, TotalCount: 1);

        var service = new Mock<IIdentityAdminService>();
        service
            .Setup(s => s.GetUsersAsync(2, 10, "ali", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetUsersQueryHandler(service.Object);

        var result = await handler.Handle(new GetUsersQuery(2, 10, "ali"), CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Handle_WhenSearchIsNull_PassesNullToService()
    {
        var expected = new PagedResult<AdminUserDto>([], Page: 1, PageSize: 50, TotalCount: 0);
        var service = new Mock<IIdentityAdminService>();
        service
            .Setup(s => s.GetUsersAsync(1, 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetUsersQueryHandler(service.Object);

        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        service.Verify(
            s => s.GetUsersAsync(1, 50, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
