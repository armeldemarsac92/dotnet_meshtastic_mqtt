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
                ITopicPresetService topicPresetService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var presets = await topicPresetService.GetTopicPresets(cancellationToken);
                    return Results.Ok(presets.Select(preset => preset.ToSavedTopicPreset()).ToList());
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
                ITopicPresetService topicPresetService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var existingPreset = await topicPresetService.GetTopicPresetByPattern(request.TopicPattern, cancellationToken);
                    var savedPreset = await topicPresetService.SaveTopicPreset(
                        request.ToSaveTopicPresetRequest(existingPreset),
                        cancellationToken);

                    return Results.Ok(savedPreset.ToSavedTopicPreset());
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
