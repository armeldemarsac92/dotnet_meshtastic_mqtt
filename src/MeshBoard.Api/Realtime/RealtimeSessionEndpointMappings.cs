using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using Microsoft.AspNetCore.Antiforgery;

namespace MeshBoard.Api.Realtime;

internal static class RealtimeSessionEndpointMappings
{
    public static IEndpointRouteBuilder MapRealtimeSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiRoutes.Realtime.Group);

        endpoints.MapGet(
            ApiRoutes.Realtime.Jwks,
            (HttpContext httpContext, IRealtimeJwksService realtimeJwksService) =>
            {
                httpContext.Response.Headers.CacheControl = "public,max-age=300";
                return Results.Ok(realtimeJwksService.GetDocument());
            });

        group.MapPost(
                ApiRoutes.Realtime.Session,
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery,
                    IRealtimeSessionService realtimeSessionService,
                    CancellationToken cancellationToken) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);

                    var scheme = httpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto)
                        ? forwardedProto.ToString().Split(',')[0].Trim()
                        : httpContext.Request.Scheme;
                    var requestOrigin = new Uri($"{scheme}://{httpContext.Request.Host}");
                    var session = await realtimeSessionService.CreateSessionAsync(requestOrigin, cancellationToken);
                    return Results.Ok(session);
                })
            .RequireAuthorization();

        return endpoints;
    }
}
