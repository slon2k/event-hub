using System.Text.Json;

namespace EventHub.Notifications.UnitTests.Functions;

public sealed class SendEmailFunctionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<ILogger<SendEmailFunction>> _logger = new();
    private readonly SendEmailFunction _sut;

    public SendEmailFunctionTests()
    {
        _emailSender
            .Setup(s => s.SendInvitationAsync(It.IsAny<InvitationSent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emailSender
            .Setup(s => s.SendCancellationAsync(It.IsAny<EventCancelled>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SendEmailFunction(_emailSender.Object, _logger.Object);
    }

    private static ServiceBusReceivedMessage CreateMessage(string? subject, string body) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            subject: subject);

    [Fact]
    public async Task Run_WhenSubjectEndsWithInvitationSent_CallsSendInvitationAsync()
    {
        var invitationId = Guid.NewGuid();
        var evt = new InvitationSent(
            EventId: Guid.NewGuid(),
            EventTitle: "Design Review",
            EventDateTime: new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero),
            EventLocation: "Room B",
            InvitationId: invitationId,
            ParticipantEmail: "alice@example.com",
            RsvpToken: "tok-abc-123",
            TokenExpiresAt: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var message = CreateMessage(
            subject: $"EventHub.Domain.Events.{nameof(InvitationSent)}",
            body: JsonSerializer.Serialize(evt, JsonOptions));

        await _sut.Run(message, CancellationToken.None);

        _emailSender.Verify(
            s => s.SendInvitationAsync(
                It.Is<InvitationSent>(e => e.InvitationId == invitationId && e.ParticipantEmail == "alice@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenSubjectEndsWithEventCancelled_CallsSendCancellationAsync()
    {
        var eventId = Guid.NewGuid();
        var evt = new EventCancelled(
            EventId: eventId,
            EventTitle: "Design Review",
            EventDateTime: new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero),
            AffectedParticipantEmails: ["alice@example.com", "bob@example.com"]);
        var message = CreateMessage(
            subject: $"EventHub.Domain.Events.{nameof(EventCancelled)}",
            body: JsonSerializer.Serialize(evt, JsonOptions));

        await _sut.Run(message, CancellationToken.None);

        _emailSender.Verify(
            s => s.SendCancellationAsync(
                It.Is<EventCancelled>(e => e.EventId == eventId && e.AffectedParticipantEmails.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenSubjectEndsWithInvitationResponded_DoesNotCallEmailSender()
    {
        var message = CreateMessage(
            subject: $"EventHub.Domain.Events.{nameof(InvitationResponded)}",
            body: "{}");

        await _sut.Run(message, CancellationToken.None);

        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenSubjectIsUnknownType_DoesNotCallEmailSenderAndDoesNotThrow()
    {
        var message = CreateMessage(
            subject: "EventHub.Domain.Events.SomeFutureEventType",
            body: "{}");

        await _sut.Run(message, CancellationToken.None);

        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Run_WhenSubjectIsNull_DoesNotCallEmailSenderAndDoesNotThrow()
    {
        var message = CreateMessage(subject: null, body: "{}");

        await _sut.Run(message, CancellationToken.None);

        _emailSender.VerifyNoOtherCalls();
    }
}
