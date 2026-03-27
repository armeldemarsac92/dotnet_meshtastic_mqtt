using MeshBoard.Api.Authentication;
using MeshBoard.Api.Preferences;
using MeshBoard.Api.Public;
using MeshBoard.Api.Realtime;
using MeshBoard.Contracts.Api;

namespace MeshBoard.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Health, () => Results.Ok(new { status = "ok" }))
            .WithName("HealthCheck")
            .Produces(200)
            .WithTags("Health");
        app.MapApiAuthEndpoints();
        app.MapBrokerPreferenceEndpoints();
        app.MapFavoritePreferenceEndpoints();
        app.MapPublicCollectorEndpoints();
        app.MapRealtimeSessionEndpoints();
        app.MapVernemqWebhookEndpoints();
        return app;
    }
}
