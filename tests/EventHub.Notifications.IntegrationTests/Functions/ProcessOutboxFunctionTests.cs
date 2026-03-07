namespace EventHub.Notifications.IntegrationTests.Functions;

[Collection("NotificationsDatabase")]
public class ProcessOutboxFunctionTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly Mock<ServiceBusClient> _clientMock = new();
    private readonly Mock<ServiceBusSender> _senderMock = new();

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
        _clientMock
            .Setup(c => c.CreateSender(It.IsAny<string>()))
            .Returns(_senderMock.Object);

        // Wipe outbox before each test so ProcessAsync sees only what the test seeds.
        await using var ctx = db.CreateContext();
        await ctx.OutboxMessages.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private ProcessOutboxFunction CreateSut(EventHubDbContext ctx)
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ServiceBusTopicName"]).Returns("notifications");
        return new ProcessOutboxFunction(
            ctx,
            _clientMock.Object,
            configMock.Object,
            Mock.Of<ILogger<ProcessOutboxFunction>>());
    }

    /// <summary>Wake-up ping dropped by the API — the message body is irrelevant to ProcessAsync.</summary>
    private static ServiceBusReceivedMessage DummyTriggerMessage() =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("{}"), subject: "ping");

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Run_WhenNoUnpublishedMessages_SendsNoMessages()
    {
        await using var ctx = db.CreateContext();
        var sut = CreateSut(ctx);

        await sut.RunOnDemand(DummyTriggerMessage(), CancellationToken.None);

        _senderMock.Verify(
            s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WhenOneUnpublishedMessage_SendsToServiceBusWithCorrectProperties()
    {
        const string payload = """{"EventId":"00000000-1111-2222-3333-444444444444"}""";
        var outbox = OutboxMessage.Create("EventHub.Domain.Events.InvitationSent", payload);

        await using (var seedCtx = db.CreateContext())
        {
            seedCtx.OutboxMessages.Add(outbox);
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        await CreateSut(ctx).RunOnDemand(DummyTriggerMessage(), CancellationToken.None);

        _senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m =>
                    m.Subject == "EventHub.Domain.Events.InvitationSent" &&
                    m.Body.ToString() == payload &&
                    m.MessageId == outbox.Id.ToString() &&
                    m.ContentType == "application/json"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WhenSendingSucceeds_MarksMessagePublishedInDatabase()
    {
        var outbox = OutboxMessage.Create("EventHub.Domain.Events.InvitationSent", "{}");

        await using (var seedCtx = db.CreateContext())
        {
            seedCtx.OutboxMessages.Add(outbox);
            await seedCtx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            await CreateSut(ctx).RunOnDemand(DummyTriggerMessage(), CancellationToken.None);
        }

        await using var readCtx = db.CreateContext();
        var persisted = await readCtx.OutboxMessages.FindAsync(outbox.Id);

        Assert.NotNull(persisted!.PublishedAt);
        Assert.Null(persisted.Error);
        Assert.Equal(0, persisted.RetryCount);
    }

    [Fact]
    public async Task Run_WhenSendingFails_MarksMessageAsFailedAndIncrementsRetryCount()
    {
        var outbox = OutboxMessage.Create("EventHub.Domain.Events.InvitationSent", "{}");

        await using (var seedCtx = db.CreateContext())
        {
            seedCtx.OutboxMessages.Add(outbox);
            await seedCtx.SaveChangesAsync();
        }

        _senderMock
            .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service Bus unavailable"));

        await using (var ctx = db.CreateContext())
        {
            await CreateSut(ctx).RunOnDemand(DummyTriggerMessage(), CancellationToken.None);
        }

        await using var readCtx = db.CreateContext();
        var persisted = await readCtx.OutboxMessages.FindAsync(outbox.Id);

        Assert.NotNull(persisted!.Error);
        Assert.Equal(1, persisted.RetryCount);
        Assert.Null(persisted.PublishedAt);
    }

    [Fact]
    public async Task Run_AlreadyPublishedMessages_AreNotResent()
    {
        var published = OutboxMessage.Create("EventHub.Domain.Events.EventCancelled", "{}");
        published.MarkPublished();

        var unpublished = OutboxMessage.Create("EventHub.Domain.Events.InvitationSent", "{}");

        await using (var seedCtx = db.CreateContext())
        {
            seedCtx.OutboxMessages.AddRange(published, unpublished);
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = db.CreateContext();
        await CreateSut(ctx).RunOnDemand(DummyTriggerMessage(), CancellationToken.None);

        // Only the unpublished message should be sent
        _senderMock.Verify(
            s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _senderMock.Verify(
            s => s.SendMessageAsync(
                It.Is<ServiceBusMessage>(m => m.MessageId == unpublished.Id.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_MultipleUnpublishedMessages_AllSentAndAllMarkedPublished()
    {
        var messages = Enumerable.Range(1, 3)
            .Select(i => OutboxMessage.Create($"EventHub.Domain.Events.InvitationSent", $"{{\"seq\":{i}}}"))
            .ToList();

        await using (var seedCtx = db.CreateContext())
        {
            seedCtx.OutboxMessages.AddRange(messages);
            await seedCtx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            await CreateSut(ctx).RunOnDemand(DummyTriggerMessage(), CancellationToken.None);
        }

        _senderMock.Verify(
            s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        await using var readCtx = db.CreateContext();
        var ids = messages.Select(m => m.Id).ToHashSet();
        var persisted = await readCtx.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

        Assert.All(persisted, m => Assert.NotNull(m.PublishedAt));
    }
}
