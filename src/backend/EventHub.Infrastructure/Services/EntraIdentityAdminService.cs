using Azure.Identity;
using EventHub.Application.Abstractions;
using EventHub.Application.Common;
using EventHub.Application.Features.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EventHub.Infrastructure.Services;

/// <summary>
/// Microsoft Graph implementation of IIdentityAdminService.
/// Uses a dedicated app registration in the identity tenant with
/// ClientSecretCredential — required because the API's managed identity
/// lives in a different (infrastructure) tenant and cannot call Graph there.
/// Configuration keys: Graph:TenantId, Graph:ClientId, Graph:ClientSecret,
/// Graph:ApiAppClientId.
/// </summary>
internal sealed class EntraIdentityAdminService : IIdentityAdminService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _apiAppClientId;

    public EntraIdentityAdminService(IConfiguration configuration)
    {
        var tenantId = configuration["Graph:TenantId"]
            ?? throw new InvalidOperationException("Graph:TenantId is not configured.");
        var clientId = configuration["Graph:ClientId"]
            ?? throw new InvalidOperationException("Graph:ClientId is not configured.");
        var clientSecret = configuration["Graph:ClientSecret"]
            ?? throw new InvalidOperationException("Graph:ClientSecret is not configured.");

        _apiAppClientId = configuration["Graph:ApiAppClientId"]
            ?? throw new InvalidOperationException("Graph:ApiAppClientId is not configured.");

        _graphClient = new GraphServiceClient(
            new ClientSecretCredential(tenantId, clientId, clientSecret),
            ["https://graph.microsoft.com/.default"]);
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(
        int page, int pageSize, string? search, CancellationToken ct)
    {
        var (spId, organizerRoleId, adminRoleId) = await GetApiSpInfoAsync(ct);
        var assignments = await GetAllAppRoleAssignmentsAsync(spId, ct);
        var organizerUserIds = BuildRoleSet(assignments, organizerRoleId);
        var adminUserIds = BuildRoleSet(assignments, adminRoleId);

        // Graph doesn't support $skip on /users — navigate to the requested page via nextLink.
        // totalCount is captured from the first response ($count=true); subsequent pages omit it.
        var response = await _graphClient.Users.GetAsync(config =>
        {
            config.QueryParameters.Top = pageSize;
            config.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
            config.QueryParameters.Orderby = ["displayName"];
            config.QueryParameters.Count = true;
            config.Headers.Add("ConsistencyLevel", "eventual");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var escaped = search.Replace("'", "''");
                config.QueryParameters.Filter =
                    $"startsWith(displayName,'{escaped}') or startsWith(userPrincipalName,'{escaped}')";
            }
        }, ct);

        int totalCount = (int)(response?.OdataCount ?? 0);

        for (int i = 1; i < page; i++)
        {
            if (response?.OdataNextLink is null)
                return new PagedResult<AdminUserDto>([], page, pageSize, totalCount);

            response = await _graphClient.Users
                .WithUrl(response.OdataNextLink)
                .GetAsync(config => config.Headers.Add("ConsistencyLevel", "eventual"), ct);
        }

        var users = response?.Value ?? [];

        var items = users.Select(u => new AdminUserDto(
            UserId: u.Id ?? "",
            DisplayName: u.DisplayName,
            Email: u.Mail ?? u.UserPrincipalName,
            IsOrganizer: organizerUserIds.Contains(u.Id ?? ""),
            IsAdmin: adminUserIds.Contains(u.Id ?? "")
        )).ToList();

        return new PagedResult<AdminUserDto>(items, page, pageSize, totalCount);
    }

    public async Task AssignOrganizerRoleAsync(
        string targetUserId, string actingAdminUserId, CancellationToken ct)
    {
        var (spId, organizerRoleId, _) = await GetApiSpInfoAsync(ct);
        var assignments = await GetAllAppRoleAssignmentsAsync(spId, ct);

        var alreadyAssigned = assignments.Any(a =>
            a.AppRoleId == organizerRoleId &&
            string.Equals(a.PrincipalId?.ToString(), targetUserId, StringComparison.OrdinalIgnoreCase));

        if (alreadyAssigned)
            return;

        await _graphClient.ServicePrincipals[spId].AppRoleAssignedTo.PostAsync(
            new AppRoleAssignment
            {
                PrincipalId = Guid.Parse(targetUserId),
                ResourceId = Guid.Parse(spId),
                AppRoleId = organizerRoleId
            }, cancellationToken: ct);
    }

    public async Task RemoveOrganizerRoleAsync(
        string targetUserId, string actingAdminUserId, CancellationToken ct)
    {
        var (spId, organizerRoleId, _) = await GetApiSpInfoAsync(ct);
        var assignments = await GetAllAppRoleAssignmentsAsync(spId, ct);

        var assignment = assignments.FirstOrDefault(a =>
            a.AppRoleId == organizerRoleId &&
            string.Equals(a.PrincipalId?.ToString(), targetUserId, StringComparison.OrdinalIgnoreCase));

        if (assignment?.Id is null)
            return;

        await _graphClient.ServicePrincipals[spId].AppRoleAssignedTo[assignment.Id]
            .DeleteAsync(cancellationToken: ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<(string SpId, Guid OrganizerRoleId, Guid AdminRoleId)> GetApiSpInfoAsync(
        CancellationToken ct)
    {
        var result = await _graphClient.ServicePrincipals.GetAsync(config =>
        {
            config.QueryParameters.Filter = $"appId eq '{_apiAppClientId}'";
            config.QueryParameters.Select = ["id", "appRoles"];
        }, ct);

        var sp = result?.Value?.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Service principal for appId '{_apiAppClientId}' not found in the identity tenant. " +
                "Ensure Graph:ApiAppClientId is correct and the Graph client has Application.Read.All permission.");

        var organizerRole = sp.AppRoles?.FirstOrDefault(r => r.Value == "Organizer")
            ?? throw new InvalidOperationException(
                "Organizer app role not found on the API service principal. " +
                "Ensure the Organizer app role is defined in the API app registration manifest.");

        var adminRole = sp.AppRoles?.FirstOrDefault(r => r.Value == "Admin");

        return (sp.Id!, organizerRole.Id ?? Guid.Empty, adminRole?.Id ?? Guid.Empty);
    }

    /// <summary>
    /// Fetches all appRoleAssignedTo entries for the API service principal,
    /// paging through nextLink until exhausted.
    /// </summary>
    private async Task<List<AppRoleAssignment>> GetAllAppRoleAssignmentsAsync(
        string spId, CancellationToken ct)
    {
        var all = new List<AppRoleAssignment>();
        var response = await _graphClient.ServicePrincipals[spId].AppRoleAssignedTo
            .GetAsync(cancellationToken: ct);

        while (response is not null)
        {
            if (response.Value is not null)
                all.AddRange(response.Value);

            if (response.OdataNextLink is null)
                break;

            response = await _graphClient.ServicePrincipals[spId].AppRoleAssignedTo
                .WithUrl(response.OdataNextLink)
                .GetAsync(cancellationToken: ct);
        }

        return all;
    }

    private static HashSet<string> BuildRoleSet(List<AppRoleAssignment> assignments, Guid roleId)
        => roleId == Guid.Empty
            ? []
            : assignments
                .Where(a => a.AppRoleId == roleId && a.PrincipalId is not null)
                .Select(a => a.PrincipalId!.Value.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
