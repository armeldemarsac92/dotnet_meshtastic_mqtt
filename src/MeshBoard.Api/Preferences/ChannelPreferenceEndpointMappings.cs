using MeshBoard.Application.Services;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Preferences;

internal static class ChannelPreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapChannelPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/preferences/channels")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async Task<IResult> (
                ISavedChannelPreferenceService savedChannelPreferenceService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var savedChannels = await savedChannelPreferenceService.GetSavedChannels(cancellationToken);
                    return Results.Ok(savedChannels);
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Active server not found", exception.Message));
                }
            });

        group.MapPost(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveChannelFilterRequest request,
                ISavedChannelPreferenceService savedChannelPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    await savedChannelPreferenceService.SaveChannel(request, cancellationToken);
                    return Results.NoContent();
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Saved channel add failed", exception.Message));
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Active server not found", exception.Message));
                }
            });

        group.MapDelete(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                string topicFilter,
                ISavedChannelPreferenceService savedChannelPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                return await DeleteSavedChannelAsync(savedChannelPreferenceService, topicFilter, cancellationToken);
            });

        group.MapDelete(
            "/{**topicFilter}",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                string topicFilter,
                ISavedChannelPreferenceService savedChannelPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                return await DeleteSavedChannelAsync(savedChannelPreferenceService, topicFilter, cancellationToken);
            });

        return endpoints;
    }

    private static async Task<IResult> DeleteSavedChannelAsync(
        ISavedChannelPreferenceService savedChannelPreferenceService,
        string topicFilter,
        CancellationToken cancellationToken)
    {
        try
        {
            await savedChannelPreferenceService.RemoveChannel(topicFilter, cancellationToken);
            return Results.NoContent();
        }
        catch (BadRequestException exception)
        {
            return Results.BadRequest(
                CreateProblemDetails(StatusCodes.Status400BadRequest, "Saved channel remove failed", exception.Message));
        }
        catch (NotFoundException exception)
        {
            return Results.NotFound(
                CreateProblemDetails(StatusCodes.Status404NotFound, "Active server not found", exception.Message));
        }
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
