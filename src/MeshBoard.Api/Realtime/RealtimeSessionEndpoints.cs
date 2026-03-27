using MeshBoard.Api.Middlewares.Validation;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Api.Realtime;

internal static class RealtimeSessionEndpoints
{
    private const string Tags = "RealtimeSessions";

    public static IEndpointRouteBuilder MapRealtimeSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ApiRoutes.Realtime.Jwks, GetJwks)
            .WithName("GetJwks")
            .Produces<JsonWebKeyDocument>()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Realtime.CreateSession, CreateRealtimeSession)
            .WithName("CreateRealtimeSession")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .Produces<RealtimeSessionResponse>()
            .Produces(400)
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        return endpoints;
    }

    private static IResult GetJwks(HttpContext httpContext, IRealtimeJwksService realtimeJwksService)
    {
        httpContext.Response.Headers.CacheControl = "public,max-age=300";
        return Results.Ok(realtimeJwksService.GetDocument());
    }

    private static async Task<IResult> CreateRealtimeSession(
        HttpContext httpContext,
        IRealtimeSessionService realtimeSessionService,
        CancellationToken cancellationToken)
    {
        var scheme = httpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto)
            ? forwardedProto.ToString().Split(',')[0].Trim()
            : httpContext.Request.Scheme;
        var requestOrigin = new Uri($"{scheme}://{httpContext.Request.Host}");
        var session = await realtimeSessionService.CreateSessionAsync(requestOrigin, cancellationToken);
        return Results.Ok(session);
    }
}
