using MeshBoard.Application.Services;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Preferences;

internal static class TopicPresetPreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapTopicPresetPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/preferences/topic-presets")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async Task<IResult> (
                IProductTopicPresetPreferenceService topicPresetPreferenceService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var presets = await topicPresetPreferenceService.GetTopicPresetPreferences(cancellationToken);
                    return Results.Ok(presets);
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        new
                        {
                            title = "Active broker not found",
                            detail = exception.Message
                        });
                }
            });

        group.MapPost(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveTopicPresetPreferenceRequest request,
                IProductTopicPresetPreferenceService topicPresetPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var savedPreset = await topicPresetPreferenceService.SaveTopicPresetPreference(
                        request,
                        cancellationToken);

                    return Results.Ok(savedPreset);
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Topic preset save failed", exception.Message));
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Active broker not found", exception.Message));
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
