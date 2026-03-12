using MeshBoard.Application.Services;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;

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

        return endpoints;
    }
}
