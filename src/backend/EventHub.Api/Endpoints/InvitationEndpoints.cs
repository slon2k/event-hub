using System.Security.Claims;
using EventHub.Api.Extensions;
using EventHub.Application.Features.Invitations.Commands.CancelInvitation;
using EventHub.Application.Features.Invitations.Commands.ReissueInvitationToken;
using EventHub.Application.Features.Invitations.Commands.RespondToInvitation;
using EventHub.Application.Features.Invitations.Commands.SendInvitation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Api.Endpoints;

public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        // -----------------------------------------------------------------------
        // Organizer-managed invitation routes (nested under /api/events)
        // -----------------------------------------------------------------------
        var organiserGroup = app
            .MapGroup("/api/events/{eventId:guid}/invitations")
            .WithTags("Invitations")
            .RequireAuthorization("OrganizerPolicy");

        organiserGroup.MapPost("/", SendInvitation)
            .WithName("SendInvitation")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        organiserGroup.MapDelete("/{invitationId:guid}", CancelInvitation)
            .WithName("CancelInvitation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        organiserGroup.MapPost("/{invitationId:guid}/reissue", ReissueInvitationToken)
            .WithName("ReissueInvitationToken")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // -----------------------------------------------------------------------
        // Public RSVP route — no authentication required (HMAC token is proof)
        // -----------------------------------------------------------------------
        app.MapPost("/api/invitations/respond", RespondToInvitation)
            .WithTags("Invitations")
            .WithName("RespondToInvitation")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> SendInvitation(
        Guid eventId,
        [FromBody] SendInvitationRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        var command = new SendInvitationCommand(eventId, request.ParticipantEmail, organizerId);
        var invitationId = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/events/{eventId}/invitations/{invitationId}", new { id = invitationId });
    }

    private static async Task<IResult> CancelInvitation(
        Guid eventId,
        Guid invitationId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        await sender.Send(new CancelInvitationCommand(eventId, invitationId, organizerId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReissueInvitationToken(
        Guid eventId,
        Guid invitationId,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var organizerId = user.GetUserId();
        await sender.Send(new ReissueInvitationTokenCommand(eventId, invitationId, organizerId), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RespondToInvitation(
        [FromBody] RespondToInvitationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InvitationResponse>(request.Response, ignoreCase: true, out var response))
        {
            return Results.Problem(
                title: "Invalid response value. Use 'Accept' or 'Decline'.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var command = new RespondToInvitationCommand(request.InvitationId, request.RawToken, response);
        await sender.Send(command, cancellationToken);
        return Results.NoContent();
    }
}

// ---------------------------------------------------------------------------
// Request contracts
// ---------------------------------------------------------------------------

public sealed record SendInvitationRequest(string ParticipantEmail);

public sealed record RespondToInvitationRequest(
    Guid InvitationId,
    string RawToken,
    string Response);
