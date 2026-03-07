using EventHub.Application.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventHub.Application.UnitTests.Behaviours;

public class LoggingBehaviourTests
{
    public record TestRequest(string Value) : IRequest<string>;

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsNextResult()
    {
        var mockLogger = new Mock<ILogger<LoggingBehaviour<TestRequest, string>>>();
        var behaviour = new LoggingBehaviour<TestRequest, string>(mockLogger.Object);

        var result = await behaviour.Handle(
            new TestRequest("ok"),
            _ => Task.FromResult("response"),
            CancellationToken.None);

        Assert.Equal("response", result);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_LogsInformation()
    {
        var mockLogger = new Mock<ILogger<LoggingBehaviour<TestRequest, string>>>();
        var behaviour = new LoggingBehaviour<TestRequest, string>(mockLogger.Object);

        await behaviour.Handle(
            new TestRequest("ok"),
            _ => Task.FromResult("response"),
            CancellationToken.None);

        // Expect two information log calls: "Handling" and "Handled"
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenNextThrows_Rethrows()
    {
        var mockLogger = new Mock<ILogger<LoggingBehaviour<TestRequest, string>>>();
        var behaviour = new LoggingBehaviour<TestRequest, string>(mockLogger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behaviour.Handle(
                new TestRequest("ok"),
                _ => throw new InvalidOperationException("boom"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenNextThrows_LogsError()
    {
        var mockLogger = new Mock<ILogger<LoggingBehaviour<TestRequest, string>>>();
        var behaviour = new LoggingBehaviour<TestRequest, string>(mockLogger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behaviour.Handle(
                new TestRequest("ok"),
                _ => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
