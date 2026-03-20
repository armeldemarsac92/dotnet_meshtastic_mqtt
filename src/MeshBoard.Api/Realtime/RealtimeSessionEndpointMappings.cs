using MeshBoard.Application.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace MeshBoard.Api.Realtime;

internal static class RealtimeSessionEndpointMappings
{
    public static IEndpointRouteBuilder MapRealtimeSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/realtime");

        endpoints.MapGet(
            "/.well-known/jwks.json",
            (HttpContext httpContext, IRealtimeJwksService realtimeJwksService) =>
            {
                httpContext.Response.Headers.CacheControl = "public,max-age=300";
                return Results.Ok(realtimeJwksService.GetDocument());
            });

        group.MapPost(
                "/session",
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery,
                    IRealtimeSessionService realtimeSessionService,
                    CancellationToken cancellationToken) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);

                    var requestOrigin = new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}");
                    var session = await realtimeSessionService.CreateSessionAsync(requestOrigin, cancellationToken);
                    return Results.Ok(session);
                })
            .RequireAuthorization();

        return endpoints;
    }
}
