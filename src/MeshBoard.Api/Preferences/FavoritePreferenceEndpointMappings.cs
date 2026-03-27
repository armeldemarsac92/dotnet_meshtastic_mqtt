using MeshBoard.Application.Services;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Favorites;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Preferences;

internal static class FavoritePreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapFavoritePreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiRoutes.Preferences.Favorites.Group)
            .RequireAuthorization();

        group.MapGet(
            ApiRoutes.Preferences.Favorites.Root,
            async Task<IResult> (
                IFavoriteNodeService favoriteNodeService,
                CancellationToken cancellationToken) =>
            {
                var favorites = await favoriteNodeService.GetFavoriteNodes(cancellationToken);
                return Results.Ok(favorites);
            });

        group.MapPost(
            ApiRoutes.Preferences.Favorites.Root,
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveFavoriteNodeRequest request,
                IFavoriteNodeService favoriteNodeService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var favorite = await favoriteNodeService.SaveFavoriteNode(request, cancellationToken);
                    return Results.Ok(favorite);
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Favorite save failed", exception.Message));
                }
            });

        group.MapDelete(
            ApiRoutes.Preferences.Favorites.ByNodeId,
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                string nodeId,
                IFavoriteNodeService favoriteNodeService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    await favoriteNodeService.RemoveFavoriteNode(nodeId, cancellationToken);
                    return Results.NoContent();
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Favorite not found", exception.Message));
                }
            });

        return endpoints;
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
    }
}
