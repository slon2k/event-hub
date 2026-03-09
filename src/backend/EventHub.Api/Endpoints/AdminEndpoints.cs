using EventHub.Application.Features.Admin;
using EventHub.Application.Features.Admin.GetAllEvents;
using MediatR;

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

        return app;
    }

    private static async Task<IResult> GetAllEvents(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAllEventsQuery(), cancellationToken);
        return Results.Ok(result);
    }
}
