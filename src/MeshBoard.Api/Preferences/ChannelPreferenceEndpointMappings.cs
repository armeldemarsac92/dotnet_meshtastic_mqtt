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
                IBrokerMonitorService brokerMonitorService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var savedChannels = await brokerMonitorService.GetSavedChannels(cancellationToken);
                    return Results.Ok(savedChannels.Select(channel => channel.ToSavedChannelFilter()).ToList());
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Active broker not found", exception.Message));
                }
            });

        group.MapPost(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveChannelFilterRequest request,
                IBrokerMonitorService brokerMonitorService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    await brokerMonitorService.SubscribeToTopic(request.TopicFilter, cancellationToken);
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
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Active broker not found", exception.Message));
                }
            });

        group.MapDelete(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                string topicFilter,
                IBrokerMonitorService brokerMonitorService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                return await DeleteSavedChannelAsync(brokerMonitorService, topicFilter, cancellationToken);
            });

        group.MapDelete(
            "/{**topicFilter}",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                string topicFilter,
                IBrokerMonitorService brokerMonitorService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                return await DeleteSavedChannelAsync(brokerMonitorService, topicFilter, cancellationToken);
            });

        return endpoints;
    }

    private static async Task<IResult> DeleteSavedChannelAsync(
        IBrokerMonitorService brokerMonitorService,
        string topicFilter,
        CancellationToken cancellationToken)
    {
        try
        {
            await brokerMonitorService.UnsubscribeFromTopic(topicFilter, cancellationToken);
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
                CreateProblemDetails(StatusCodes.Status404NotFound, "Active broker not found", exception.Message));
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
