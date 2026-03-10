using EventHub.Api.Extensions;
using EventHub.Application.Common;
using EventHub.Application.Features.Admin;
using EventHub.Application.Features.Admin.AssignOrganizerRole;
using EventHub.Application.Features.Admin.GetAllEvents;
using EventHub.Application.Features.Admin.GetUsers;
using EventHub.Application.Features.Admin.RemoveOrganizerRole;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventHub.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("AdminPolicy");

        group.MapGet("/events", GetAllEvents)
            .WithName("AdminGetAllEvents")
            .Produces<IReadOnlyList<AdminEventSummaryDto>>();

        group.MapGet("/users", GetUsers)
            .WithName("AdminGetUsers")
            .Produces<PagedResult<AdminUserDto>>();

        group.MapPost("/users/{userId}/roles/organizer", AssignOrganizerRole)
            .WithName("AdminAssignOrganizerRole")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapDelete("/users/{userId}/roles/organizer", RemoveOrganizerRole)
            .WithName("AdminRemoveOrganizerRole")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetAllEvents(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAllEventsQuery(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUsers(
        ISender sender,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetUsersQuery(page, pageSize, search), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> AssignOrganizerRole(
        string userId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var actingAdminId = user.GetUserId();
        await sender.Send(new AssignOrganizerRoleCommand(userId, actingAdminId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveOrganizerRole(
        string userId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var actingAdminId = user.GetUserId();
        await sender.Send(new RemoveOrganizerRoleCommand(userId, actingAdminId), cancellationToken);
        return Results.NoContent();
    }
}
