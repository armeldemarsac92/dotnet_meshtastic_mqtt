using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Preferences;

internal static class BrokerPreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapBrokerPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            ApiRoutes.Preferences.Brokers.GetAll,
            async Task<IResult> (
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                var profiles = await brokerPreferenceService.GetBrokerPreferences(cancellationToken);
                return Results.Ok(profiles);
            })
            .RequireAuthorization();

        endpoints.MapPost(
            ApiRoutes.Preferences.Brokers.Create,
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveBrokerPreferenceRequest request,
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var savedProfile = await brokerPreferenceService.CreateBrokerPreference(
                        request,
                        cancellationToken);

                    return Results.Created(
                        $"{ApiRoutes.Preferences.Brokers.GetAll}/{savedProfile.Id}",
                        savedProfile);
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Server save failed", exception.Message));
                }
            })
            .RequireAuthorization();

        endpoints.MapPut(
            ApiRoutes.Preferences.Brokers.Update,
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                Guid id,
                SaveBrokerPreferenceRequest request,
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var existingProfile = await brokerPreferenceService.GetBrokerPreferenceById(id, cancellationToken);
                    if (existingProfile is null)
                    {
                        return Results.NotFound(
                            CreateProblemDetails(
                                StatusCodes.Status404NotFound,
                                "Server not found",
                                $"Server profile '{id}' was not found."));
                    }

                    var savedProfile = await brokerPreferenceService.UpdateBrokerPreference(
                        id,
                        request,
                        cancellationToken);

                    return Results.Ok(savedProfile);
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Server save failed", exception.Message));
                }
            })
            .RequireAuthorization();

        endpoints.MapGet(
            ApiRoutes.Preferences.Brokers.GetActive,
            async Task<IResult> (
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var activeProfile = await brokerPreferenceService.GetActiveBrokerPreference(cancellationToken);
                    return Results.Ok(activeProfile);
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        new
                        {
                            title = "Active server not found",
                            detail = exception.Message
                        });
                }
            })
            .RequireAuthorization();

        endpoints.MapPost(
            ApiRoutes.Preferences.Brokers.Activate,
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                Guid id,
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var activeProfile = await brokerPreferenceService.ActivateBrokerPreference(id, cancellationToken);
                    return Results.Ok(activeProfile);
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Server not found", exception.Message));
                }
            })
            .RequireAuthorization();

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
