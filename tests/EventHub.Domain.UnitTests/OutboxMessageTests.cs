namespace EventHub.Domain.UnitTests;

public class OutboxMessageTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var before = DateTimeOffset.UtcNow;

        var message = OutboxMessage.Create("InvitationSent", """{"key":"value"}""");

        Assert.Equal("InvitationSent", message.Type);
        Assert.Equal("""{"key":"value"}""", message.Payload);
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.True(message.CreatedAt >= before);
        Assert.Null(message.PublishedAt);
        Assert.Null(message.Error);
        Assert.Equal(0, message.RetryCount);
    }

    [Fact]
    public void MarkPublished_SetsPublishedAtAndClearsError()
    {
        var message = OutboxMessage.Create("InvitationSent", "{}");
        message.MarkFailed("previous error");
        var before = DateTimeOffset.UtcNow;

        message.MarkPublished();

        Assert.NotNull(message.PublishedAt);
        Assert.True(message.PublishedAt >= before);
        Assert.Null(message.Error);
    }

    [Fact]
    public void MarkFailed_IncrementsRetryCountAndSetsError()
    {
        var message = OutboxMessage.Create("InvitationSent", "{}");

        message.MarkFailed("connection timeout");

        Assert.Equal(1, message.RetryCount);
        Assert.Equal("connection timeout", message.Error);
        Assert.Null(message.PublishedAt);
    }

    [Fact]
    public void MarkFailed_CalledMultipleTimes_AccumulatesRetryCount()
    {
        var message = OutboxMessage.Create("InvitationSent", "{}");

        message.MarkFailed("error 1");
        message.MarkFailed("error 2");
        message.MarkFailed("error 3");

        Assert.Equal(3, message.RetryCount);
        Assert.Equal("error 3", message.Error);
    }
}
