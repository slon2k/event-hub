using System.Net.Http.Headers;

namespace EventHub.Api.E2ETests;

/// <summary>
/// Shared fixture that creates HTTP clients from environment variables.
/// Required: EVENTHUB_TEST_HOST, EVENTHUB_TEST_ORGANIZER_TOKEN, EVENTHUB_TEST_ADMIN_TOKEN.
/// Optional: EVENTHUB_TEST_TARGET_USER_ID — a real user ID in the target Entra tenant used
///           by role management tests. Those tests are silently skipped when it is not set.
/// </summary>
public sealed class E2EFixture : IDisposable
{
    public HttpClient AnonymousClient { get; }
    public HttpClient OrganizerClient { get; }
    public HttpClient AdminClient { get; }

    /// <summary>Raw admin bearer token — used to extract the admin's own OID for self-assign tests.</summary>
    public string AdminToken { get; }

    /// <summary>
    /// OID of a test user in the target Entra tenant used as the role-assignment target.
    /// Null when EVENTHUB_TEST_TARGET_USER_ID is not set; role management tests skip themselves in that case.
    /// </summary>
    public string? TargetUserId { get; } =
        Environment.GetEnvironmentVariable("EVENTHUB_TEST_TARGET_USER_ID");

    public E2EFixture()
    {
        var host = RequiredEnv("EVENTHUB_TEST_HOST").TrimEnd('/');
        var organizerToken = RequiredEnv("EVENTHUB_TEST_ORGANIZER_TOKEN");
        AdminToken = RequiredEnv("EVENTHUB_TEST_ADMIN_TOKEN");

        AnonymousClient = new HttpClient { BaseAddress = new Uri(host) };
        OrganizerClient = CreateClient(host, organizerToken);
        AdminClient = CreateClient(host, AdminToken);
    }

    public void Dispose()
    {
        AnonymousClient.Dispose();
        OrganizerClient.Dispose();
        AdminClient.Dispose();
    }

    /// <summary>Extracts the 'oid' claim from a JWT without signature verification.</summary>
    public static string GetOidFromToken(string token)
    {
        var segments = token.Split('.');
        if (segments.Length < 2)
            throw new InvalidOperationException("Token is not a valid JWT.");

        var payload = segments[1];
        // Pad to a multiple of 4 for base64 decoding.
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("oid").GetString()
            ?? throw new InvalidOperationException("'oid' claim is null in the token payload.");
    }

    private static HttpClient CreateClient(string host, string token)
    {
        var client = new HttpClient { BaseAddress = new Uri(host) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RequiredEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException(
                $"Required environment variable '{name}' is not set. " +
                "E2E tests require: EVENTHUB_TEST_HOST, EVENTHUB_TEST_ORGANIZER_TOKEN, EVENTHUB_TEST_ADMIN_TOKEN.");
}

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture> { }

// ---------------------------------------------------------------------------
// Shared response contracts
// ---------------------------------------------------------------------------

internal sealed record IdResponse(Guid Id);

internal sealed record PagedResultResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

internal sealed record AdminUserResponse(
    string UserId,
    string? DisplayName,
    string? Email,
    bool IsOrganizer,
    bool IsAdmin);
