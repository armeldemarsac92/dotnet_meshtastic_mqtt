using MeshBoard.Application.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace MeshBoard.Api.Realtime;

internal static class RealtimeSessionEndpointMappings
{
    public static IEndpointRouteBuilder MapRealtimeSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/realtime");

        group.MapPost(
                "/session",
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery,
                    IRealtimeSessionService realtimeSessionService,
                    CancellationToken cancellationToken) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);

                    var session = await realtimeSessionService.CreateSessionAsync(cancellationToken);
                    return Results.Ok(session);
                })
            .RequireAuthorization();

        return endpoints;
    }
}
