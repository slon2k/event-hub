using System.Security.Claims;
using EventHub.Api.Extensions;
using EventHub.Application.Features.Events.Commands.CancelEvent;
using EventHub.Application.Features.Events.Commands.CreateEvent;
using EventHub.Application.Features.Events.Commands.PublishEvent;
using EventHub.Application.Features.Events.Commands.UpdateEvent;
using EventHub.Application.Features.Events.Queries.GetEventById;
using EventHub.Application.Features.Events.Queries;
using EventHub.Application.Features.Events.Queries.GetMyEvents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/events")
            .WithTags("Events")
            .RequireAuthorization("OrganizerPolicy");

        group.MapGet("/", GetMyEvents)
            .WithName("GetMyEvents")
            .Produces<IReadOnlyList<EventSummaryDto>>();

        group.MapGet("/{id:guid}", GetEventById)
            .WithName("GetEventById")
            .Produces<EventDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateEvent)
            .WithName("CreateEvent")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}", UpdateEvent)
            .WithName("UpdateEvent")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/publish", PublishEvent)
            .WithName("PublishEvent")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", CancelEvent)
            .WithName("CancelEvent")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetMyEvents(
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        var result = await sender.Send(new GetMyEventsQuery(organizerId), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEventById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEventByIdQuery(id), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateEvent(
        [FromBody] CreateEventRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        var command = new CreateEventCommand(
            request.Title,
            request.Description,
            request.DateTime,
            request.Location,
            request.Capacity,
            organizerId);

        var eventId = await sender.Send(command, cancellationToken);
        return Results.CreatedAtRoute("GetEventById", new { id = eventId }, new { id = eventId });
    }

    private static async Task<IResult> UpdateEvent(
        Guid id,
        [FromBody] UpdateEventRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        var command = new UpdateEventCommand(
            id,
            request.Title,
            request.Description,
            request.DateTime,
            request.Location,
            request.Capacity,
            organizerId);

        await sender.Send(command, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PublishEvent(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        await sender.Send(new PublishEventCommand(id, organizerId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelEvent(
        Guid id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        await sender.Send(new CancelEventCommand(id, organizerId), cancellationToken);
        return Results.NoContent();
    }
}

// ---------------------------------------------------------------------------
// Request contracts
// ---------------------------------------------------------------------------

public sealed record CreateEventRequest(
    string Title,
    string? Description,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity);

public sealed record UpdateEventRequest(
    string Title,
    string? Description,
    DateTimeOffset DateTime,
    string? Location,
    int? Capacity);
