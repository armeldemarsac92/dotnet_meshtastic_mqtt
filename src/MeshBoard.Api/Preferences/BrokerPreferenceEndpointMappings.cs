using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Preferences;

internal static class BrokerPreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapBrokerPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/preferences/brokers")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async Task<IResult> (
                IProductBrokerPreferenceService brokerPreferenceService,
                CancellationToken cancellationToken) =>
            {
                var profiles = await brokerPreferenceService.GetBrokerPreferences(cancellationToken);
                return Results.Ok(profiles);
            });

        group.MapPost(
            "/",
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
                        $"/api/preferences/brokers/{savedProfile.Id}",
                        savedProfile);
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Broker save failed", exception.Message));
                }
            });

        group.MapPut(
            "/{id:guid}",
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
                                "Broker not found",
                                $"Broker server profile '{id}' was not found."));
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
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Broker save failed", exception.Message));
                }
            });

        group.MapGet(
            "/active",
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
                            title = "Active broker not found",
                            detail = exception.Message
                        });
                }
            });

        group.MapPost(
            "/{id:guid}/activate",
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
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Broker not found", exception.Message));
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
