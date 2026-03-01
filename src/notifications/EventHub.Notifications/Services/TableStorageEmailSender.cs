using Azure.Data.Tables;
using EventHub.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventHub.Notifications.Services;

/// <summary>
/// Development stub that writes emails to an Azure Table Storage table instead of
/// sending via ACS. Rows can be inspected in Azure Storage Explorer, the VS Code
/// Azure extension, or the Azure Portal — no email client or log trawling required.
///
/// Enable by setting <c>AcsEmail:UseStub=true</c> in <c>local.settings.json</c>
/// (local) or as a Function App application setting (dev environment).
///
/// Table: configured by <c>EmailOutboxTableName</c> (default: <c>EmailOutbox</c>).
/// Locally the table is created automatically in Azurite.
/// In Azure the function app managed identity must have Storage Table Data Contributor
/// on the storage account — this role is granted by the Bicep infra.
///
/// Schema (InvitationSent):
///   PartitionKey = EventId | RowKey = InvitationId (stable — reissues update, not duplicate)
///   MessageType, To, Subject, RsvpToken, AcceptUrl, DeclineUrl, InvitationId, SentAt, TokenExpiresAt
///
/// Schema (EventCancelled — one row per recipient):
///   PartitionKey = EventId | RowKey = new Guid
///   MessageType, To, Subject, SentAt
/// </summary>
public sealed class TableStorageEmailSender(
    TableServiceClient tableServiceClient,
    IConfiguration configuration,
    ILogger<TableStorageEmailSender> logger) : IEmailSender
{
    private readonly string _tableName =
        configuration["EmailOutboxTableName"] ?? "EmailOutbox";

    private readonly string _appBaseUrl =
        configuration["App__BaseUrl"] ?? "https://eventhub.example.com";

    // Lazily initialised on first use; safe because the sender is registered as a singleton.
    private TableClient? _tableClient;

    public async Task SendInvitationAsync(InvitationSent evt, CancellationToken cancellationToken = default)
    {
        var client = await GetTableClientAsync(cancellationToken);

        var acceptUrl  = $"{_appBaseUrl}/rsvp/{evt.InvitationId}?token={Uri.EscapeDataString(evt.RsvpToken)}&response=Accept";
        var declineUrl = $"{_appBaseUrl}/rsvp/{evt.InvitationId}?token={Uri.EscapeDataString(evt.RsvpToken)}&response=Decline";

        var entity = new TableEntity(evt.EventId.ToString(), evt.InvitationId.ToString())
        {
            ["MessageType"]    = "InvitationSent",
            ["To"]             = evt.ParticipantEmail,
            ["Subject"]        = $"You're invited: {evt.EventTitle}",
            ["RsvpToken"]      = evt.RsvpToken,
            ["AcceptUrl"]      = acceptUrl,
            ["DeclineUrl"]     = declineUrl,
            ["InvitationId"]   = evt.InvitationId.ToString(),
            ["SentAt"]         = DateTimeOffset.UtcNow,
            ["TokenExpiresAt"] = evt.TokenExpiresAt,
        };

        // UpsertEntity (InsertOrReplace) so token reissues update the row rather than
        // creating a duplicate for the same invitation.
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        logger.LogInformation(
            "[TABLE STUB] Invitation stored → table={Table} | To={To} | InvitationId={InvitationId} | AcceptUrl={AcceptUrl}",
            _tableName, evt.ParticipantEmail, evt.InvitationId, acceptUrl);
    }

    public async Task SendCancellationAsync(EventCancelled evt, CancellationToken cancellationToken = default)
    {
        var client = await GetTableClientAsync(cancellationToken);

        foreach (var email in evt.AffectedParticipantEmails)
        {
            var entity = new TableEntity(evt.EventId.ToString(), Guid.NewGuid().ToString())
            {
                ["MessageType"] = "EventCancelled",
                ["To"]          = email,
                ["Subject"]     = $"Cancelled: {evt.EventTitle}",
                ["SentAt"]      = DateTimeOffset.UtcNow,
            };

            // AddEntity (Insert) — multiple recipients each get their own row.
            await client.AddEntityAsync(entity, cancellationToken);

            logger.LogInformation(
                "[TABLE STUB] Cancellation stored → table={Table} | To={To} | EventId={EventId}",
                _tableName, email, evt.EventId);
        }
    }

    private async Task<TableClient> GetTableClientAsync(CancellationToken cancellationToken)
    {
        if (_tableClient is not null)
            return _tableClient;

        var client = tableServiceClient.GetTableClient(_tableName);
        await client.CreateIfNotExistsAsync(cancellationToken);
        _tableClient = client;
        return _tableClient;
    }
}
