using EventHub.Domain.Common;

namespace EventHub.Domain.Entities;

/// <summary>
/// Durable staging area for domain event payloads that must be published to Azure Service Bus. 
/// Written in the same EF Core transaction as the domain change
/// </summary>
public sealed class OutboxMessage : Entity
{
    /// <summary>Short type discriminator, e.g. "InvitationSent" or "EventCancelled".</summary>
    public string Type { get; private set; } = default!;

    /// <summary>JSON-serialized domain event payload.</summary>
    public string Payload { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Null until ProcessOutboxFunction successfully publishes to Service Bus.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Last error message from a failed publish attempt.</summary>
    public string? Error { get; private set; }

    /// <summary>Number of publish attempts made by ProcessOutboxFunction.</summary>
    public int RetryCount { get; private set; }

    private OutboxMessage() { } // EF Core

    public static OutboxMessage Create(string type, string payload) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public void MarkPublished()
    {
        PublishedAt = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
    }
}
