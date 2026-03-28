using MeshBoard.Api.Middlewares.Validation;
using MeshBoard.Application.Preferences;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Api.Preferences;

internal static class FavoritePreferenceEndpoints
{
    private const string Tags = "FavoritePreferences";
    private const string ContentType = "application/json";

    public static IEndpointRouteBuilder MapFavoritePreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ApiRoutes.Preferences.Favorites.GetAll, GetFavoriteNodes)
            .WithName("GetFavoriteNodes")
            .Produces<IReadOnlyCollection<FavoriteNode>>()
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Preferences.Favorites.Save, SaveFavoriteNode)
            .WithName("SaveFavoriteNode")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithValidator<SaveFavoriteNodeRequest>()
            .Accepts<SaveFavoriteNodeRequest>(ContentType)
            .Produces<FavoriteNode>()
            .Produces(400)
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapDelete(ApiRoutes.Preferences.Favorites.Remove, RemoveFavoriteNode)
            .WithName("RemoveFavoriteNode")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .Produces(204)
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .RequireAuthorization()
            .WithTags(Tags);

        return endpoints;
    }

    private static async Task<IResult> GetFavoriteNodes(
        IFavoriteNodeService favoriteNodeService,
        CancellationToken cancellationToken)
    {
        var favorites = await favoriteNodeService.GetFavoriteNodes(cancellationToken);
        return Results.Ok(favorites);
    }

    private static async Task<IResult> SaveFavoriteNode(
        SaveFavoriteNodeRequest request,
        IFavoriteNodeService favoriteNodeService,
        CancellationToken cancellationToken)
    {
        var favorite = await favoriteNodeService.SaveFavoriteNode(request, cancellationToken);
        return Results.Ok(favorite);
    }

    private static async Task<IResult> RemoveFavoriteNode(
        string nodeId,
        IFavoriteNodeService favoriteNodeService,
        CancellationToken cancellationToken)
    {
        await favoriteNodeService.RemoveFavoriteNode(nodeId, cancellationToken);
        return Results.NoContent();
    }
}
